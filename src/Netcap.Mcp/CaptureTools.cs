// Licensed under the Apache License, Version 2.0.

namespace Netcap.Mcp;

using System.ComponentModel;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using Netcap.Capture;
using Netcap.Formats;
using Netcap.Models;
using PacketDotNet;
using SharpPcap.LibPcap;

/// <summary>
/// MCP tools that expose network capture functionality.
/// </summary>
[McpServerToolType]
internal sealed class CaptureTools
{
    private CaptureTools()
    {
    }

    private const long kMaxResponseBytes = 10L * 1024 * 1024;
    private const int kMaxCaptureNowSeconds = 60;
    private const int kMaxStartCaptureSeconds = 30 * 60;

    [McpServerTool(Name = "list_interfaces")]
    [Description("Lists local network interfaces that can be used as the " +
        "'interfaceName' parameter to start_capture with source='pcap'.")]
    public static IReadOnlyList<NetworkInterfaceInfo> ListInterfaces()
    {
        try
        {
            var devices = LibPcapLiveDeviceList.Instance;
            var result = new List<NetworkInterfaceInfo>(devices.Count);
            foreach (var d in devices)
            {
                result.Add(DescribeDevice(d));
            }
            return result;
        }
        catch (Exception ex)
        {
            throw new NetcapException(
                "Unable to enumerate network interfaces (libpcap/Npcap installed " +
                "and process has required privileges?): " + ex.Message, ex);
        }
    }

    private static NetworkInterfaceInfo DescribeDevice(LibPcapLiveDevice d)
    {
        string? linkType = null;
        try
        {
            linkType = d.LinkType.ToString();
        }
        catch
        {
            // Some devices report LinkType only when opened; tolerate.
        }
        var addresses = new List<string>();
        try
        {
            foreach (var a in d.Addresses ?? [])
            {
                var ip = a?.Addr?.ipAddress;
                if (ip is not null)
                {
                    addresses.Add(ip.ToString());
                }
            }
        }
        catch
        {
            // Tolerate address enumeration failures.
        }
        return new NetworkInterfaceInfo
        {
            Name = d.Name,
            FriendlyName = SafeGet(() => d.Interface?.FriendlyName),
            Description = SafeGet(() => d.Description),
            Addresses = addresses,
            LinkType = linkType,
            IsLoopback = SafeGet(() => (bool?)d.Loopback) ?? false
        };
    }

    private static T? SafeGet<T>(Func<T?> func)
    {
        try { return func(); } catch { return default; }
    }

    [McpServerTool(Name = "start_capture")]
    [Description("Starts a new capture session. For source='pcap' supply " +
        "'interfaceName' (use list_interfaces) and optional 'bpfFilter'. " +
        "For source='http' supply optional 'listenPort' (default ephemeral). " +
        "Returns the session id which must be passed to stop_capture / " +
        "get_capture / summarize_capture. The session captures until " +
        "stop_capture is called or until the configured limits are reached.")]
    public static async Task<CaptureSessionInfo> StartCaptureAsync(
        CaptureSessionManager manager,
        [Description("The capture request, including the source name and " +
            "source-specific parameters.")]
        StartCaptureRequest request,
        CancellationToken ct)
    {
        var validated = ValidateAndClampStart(request);
        var session = manager.CreateSession(validated);
        try
        {
            await session.StartAsync(ct).ConfigureAwait(false);
        }
        catch
        {
            await manager.RemoveAsync(session.Id, CancellationToken.None)
                .ConfigureAwait(false);
            throw;
        }
        return session.ToInfo();
    }

    [McpServerTool(Name = "stop_capture")]
    [Description("Stops a running capture session and finalises the trace " +
        "on disk. Subsequent calls to get_capture / summarize_capture are " +
        "safe once stop_capture returns.")]
    public static async Task<CaptureSessionInfo> StopCaptureAsync(
        CaptureSessionManager manager,
        [Description("Session id returned by start_capture.")] string sessionId,
        CancellationToken ct)
    {
        var session = manager.Get(sessionId);
        await session.StopAsync(ct).ConfigureAwait(false);
        return session.ToInfo();
    }

    [McpServerTool(Name = "list_captures")]
    [Description("Lists capture sessions known to the server. Filter with " +
        "'state' (active|completed|all). Defaults to 'all'.")]
    public static IReadOnlyList<CaptureSessionInfo> ListCaptures(
        CaptureSessionManager manager,
        [Description("Optional filter: 'active' (starting/running), " +
            "'completed' (completed/failed), or 'all'.")]
        string? state = null)
    {
        var s = state is null
            ? "all"
            : state.Trim();
        IEnumerable<CaptureSession> sessions = manager.List();
        sessions = s switch
        {
            _ when s.Equals("active", StringComparison.OrdinalIgnoreCase) =>
                sessions.Where(x => x.State is CaptureSessionState.Starting
                    or CaptureSessionState.Running),
            _ when s.Equals("completed", StringComparison.OrdinalIgnoreCase) =>
                sessions.Where(x => x.State is CaptureSessionState.Completed
                    or CaptureSessionState.Failed),
            _ when s.Equals("all", StringComparison.OrdinalIgnoreCase) ||
                s.Length == 0 => sessions,
            _ => throw new NetcapException(
                $"Unknown state filter '{state}'. Use active|completed|all.")
        };
        return [.. sessions.Select(x => x.ToInfo())];
    }

    [McpServerTool(Name = "get_capture")]
    [Description("Returns the captured trace in the requested format. " +
        "Binary formats (pcap, pcapng) are returned as an embedded " +
        "resource block; text formats (json, csv, text) are returned as " +
        "text content. Default format is 'pcap'. By default the session " +
        "must be stopped first; set allowPartial=true to read a snapshot " +
        "of an active session (no stop).")]
    public static async Task<IList<ContentBlock>> GetCaptureAsync(
        CaptureSessionManager manager, TraceFormatterRegistry formatters,
        [Description("Session id.")] string sessionId,
        [Description("Output format: pcap|pcapng|json|csv|text.")]
        string format = "pcap",
        [Description("Maximum packets to format. Defaults to all.")]
        long? maxPackets = null,
        [Description("If true, format a snapshot of an active session " +
            "without stopping it. Default false.")]
        bool allowPartial = false,
        CancellationToken ct = default)
    {
        if (!FormatKindExtensions.TryParse(format, out var kind))
        {
            throw new NetcapException(
                $"Unknown format '{format}'. Use pcap|pcapng|json|csv|text.");
        }
        var session = manager.Get(sessionId);
        EnsureFormatSupported(session, kind);
        EnsureReadable(session, allowPartial);

        using var _ = await session.AcquireAsync(ct).ConfigureAwait(false);
        var formatter = formatters.Get(kind);
        var result = await formatter.FormatAsync(session, maxPackets, ct)
            .ConfigureAwait(false);
        return BuildContent(session, result);
    }

    [McpServerTool(Name = "capture_now")]
    [Description("Convenience: start a capture, wait for 'durationSeconds' " +
        "(capped at 60s), stop, then return the formatted trace in one " +
        "call. Cleanup is guaranteed even on cancellation.")]
    public static async Task<IList<ContentBlock>> CaptureNowAsync(
        CaptureSessionManager manager, TraceFormatterRegistry formatters,
        [Description("Capture configuration, format, and duration.")]
        CaptureNowRequest request,
        CancellationToken ct)
    {
        if (request.DurationSeconds <= 0)
        {
            throw new NetcapException("durationSeconds must be > 0.");
        }
        if (request.DurationSeconds > kMaxCaptureNowSeconds)
        {
            throw new NetcapException(
                $"durationSeconds must be <= {kMaxCaptureNowSeconds}. " +
                "Use start_capture for longer captures.");
        }
        if (!FormatKindExtensions.TryParse(request.Format, out var kind))
        {
            throw new NetcapException(
                $"Unknown format '{request.Format}'. Use pcap|pcapng|json|csv|text.");
        }
        var start = ValidateAndClampStart(request.ToStartRequest());
        var session = manager.CreateSession(start);
        try
        {
            EnsureFormatSupported(session, kind);
            await session.StartAsync(ct).ConfigureAwait(false);
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(request.DurationSeconds), ct)
                    .ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                // Capture what we have so far.
            }
            await session.StopAsync(CancellationToken.None).ConfigureAwait(false);

            using var _ = await session.AcquireAsync(CancellationToken.None)
                .ConfigureAwait(false);
            var formatter = formatters.Get(kind);
            var result = await formatter.FormatAsync(session, request.MaxPackets,
                CancellationToken.None).ConfigureAwait(false);
            return BuildContent(session, result);
        }
        finally
        {
            await manager.RemoveAsync(session.Id, CancellationToken.None)
                .ConfigureAwait(false);
        }
    }

    [McpServerTool(Name = "summarize_capture")]
    [Description("Returns counts plus top-N talkers (source/destination), " +
        "protocols and ports for a stopped session.")]
    public static async Task<CaptureSummary> SummarizeAsync(
        CaptureSessionManager manager,
        [Description("Session id.")] string sessionId,
        [Description("Top-N entries to return per category. Default 10.")]
        int topN = 10,
        CancellationToken ct = default)
    {
        var session = manager.Get(sessionId);
        EnsureReadable(session, allowPartial: false);
        using var _ = await session.AcquireAsync(ct).ConfigureAwait(false);
        return await CaptureSummarizer.SummarizeAsync(session, topN, ct)
            .ConfigureAwait(false);
    }

    private static StartCaptureRequest ValidateAndClampStart(StartCaptureRequest r)
    {
        if (string.IsNullOrWhiteSpace(r.Source))
        {
            throw new NetcapException("'source' is required.");
        }
        var src = r.Source.Trim();
        if (src.Equals(CaptureSourceNames.Pcap, StringComparison.OrdinalIgnoreCase))
        {
            src = CaptureSourceNames.Pcap;
            if (string.IsNullOrWhiteSpace(r.InterfaceName))
            {
                throw new NetcapException(
                    "source='pcap' requires 'interfaceName'.");
            }
        }
        else if (src.Equals(CaptureSourceNames.Http, StringComparison.OrdinalIgnoreCase))
        {
            src = CaptureSourceNames.Http;
            if (r.ListenPort is < 0 or > 65535)
            {
                throw new NetcapException(
                    "'listenPort' must be in range 0..65535.");
            }
        }
        else
        {
            throw new NetcapException(
                $"Unknown source '{r.Source}'. Use 'pcap' or 'http'.");
        }
        var duration = r.MaxDurationSeconds is int d && d > 0 && d < kMaxStartCaptureSeconds
            ? d : kMaxStartCaptureSeconds;
        return r with { Source = src, MaxDurationSeconds = duration };
    }

    private static void EnsureFormatSupported(CaptureSession session, FormatKind kind)
    {
        if (!session.Source.SupportedFormats.Contains(kind))
        {
            var supported = string.Join(", ",
                session.Source.SupportedFormats.Select(f => f.ToWireName()));
            throw new NetcapException(
                $"Source '{session.SourceName}' does not support format " +
                $"'{kind.ToWireName()}'. Supported: {supported}.");
        }
    }

    private static void EnsureReadable(CaptureSession session, bool allowPartial)
    {
        if (session.State is CaptureSessionState.Disposed)
        {
            throw new NetcapException(
                $"Session '{session.Id}' has been disposed.");
        }
        if (!allowPartial && session.State is CaptureSessionState.Starting
            or CaptureSessionState.Running)
        {
            throw new NetcapException(
                $"Session '{session.Id}' is still {session.State}. " +
                "Call stop_capture first, or pass allowPartial=true.");
        }
    }

    private static List<ContentBlock> BuildContent(CaptureSession session,
        FormatResult result)
    {
        if (result.ByteSize > kMaxResponseBytes)
        {
            throw new NetcapException(
                $"Formatted response is {result.ByteSize:N0} bytes which " +
                $"exceeds the {kMaxResponseBytes:N0} byte limit. " +
                "Lower 'maxPackets', narrow the filter, or pick a smaller format.");
        }

        var blocks = new List<ContentBlock>
        {
            new TextContentBlock
            {
                Text = $"sessionId={session.Id} source={session.SourceName} " +
                    $"format={result.Kind.ToWireName()} " +
                    $"packets={result.PacketsFormatted} " +
                    $"bytes={result.ByteSize}"
            }
        };

        if (result.IsBinary)
        {
            blocks.Add(new EmbeddedResourceBlock
            {
                Resource = BlobResourceContents.FromBytes(
                    result.Bytes!,
                    $"netcap://sessions/{session.Id}/{result.Kind.ToWireName()}",
                    result.MimeType)
            });
        }
        else
        {
            blocks.Add(new TextContentBlock { Text = result.Text ?? string.Empty });
        }
        return blocks;
    }
}
