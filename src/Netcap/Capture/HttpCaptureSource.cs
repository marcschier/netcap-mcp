// Licensed under the Apache License, Version 2.0.

namespace Netcap.Capture;

using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Netcap.Models;
using PacketDotNet;

/// <summary>
/// Passive HTTP capture source. Binds a loopback <see cref="HttpListener"/>,
/// records inbound request metadata to a JSONL file, and returns
/// <c>204 No Content</c> to every request. Does NOT forward requests, so it
/// cannot be abused as an open proxy.
/// </summary>
internal sealed class HttpCaptureSource : ICaptureSource
{
    private const int kDefaultMaxBytes = 50 * 1024 * 1024;
    private const int kDefaultMaxDurationSeconds = 30 * 60;
    private const string kEventsFileName = "events.jsonl";

    private readonly ILogger _logger;
    private readonly string _folder;
    private readonly string _filePath;
    private readonly Lock _writeLock = new();

    private HttpListener? _listener;
    private CancellationTokenSource? _loopCts;
    private Task? _loopTask;
    private FileStream? _writeStream;
    private long _packetCount;
    private long _byteCount;
    private long _maxBytes;
    private long _maxPackets;
    private DateTimeOffset _startedAt;
    private TimeSpan _maxDuration;
    private string? _listenUrl;

    public HttpCaptureSource(string sessionFolder, ILogger logger)
    {
        _folder = sessionFolder;
        _filePath = Path.Combine(sessionFolder, kEventsFileName);
        _logger = logger;
    }

    public LinkLayers LinkType => LinkLayers.Null;

    public IReadOnlySet<FormatKind> SupportedFormats { get; } =
        new HashSet<FormatKind>
        {
            FormatKind.Json, FormatKind.Csv, FormatKind.Text
        };

    public long PacketCount => Interlocked.Read(ref _packetCount);

    public long ByteCount => Interlocked.Read(ref _byteCount);

    public string? ListenUrl => _listenUrl;

    public string? GetRawPcapFilePath() => null;

    public Task StartAsync(StartCaptureRequest request, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        var port = request.ListenPort ?? 0;
        if (port < 0 || port > 65535)
        {
            throw new NetcapException(
                $"listenPort must be in range 0..65535 (got {port}).");
        }
        var pathPrefix = (request.PathPrefix ?? "/").Trim();
        if (!pathPrefix.StartsWith('/'))
        {
            pathPrefix = "/" + pathPrefix;
        }
        if (!pathPrefix.EndsWith('/'))
        {
            pathPrefix += "/";
        }

        _maxBytes = request.MaxBytes ?? kDefaultMaxBytes;
        _maxPackets = request.MaxPackets ?? long.MaxValue;
        _maxDuration = TimeSpan.FromSeconds(
            request.MaxDurationSeconds ?? kDefaultMaxDurationSeconds);

        if (port == 0)
        {
            port = GetEphemeralPort();
        }
        var prefix = $"http://127.0.0.1:{port}{pathPrefix}";
        var listener = new HttpListener();
        listener.Prefixes.Add(prefix);
        try
        {
            listener.Start();
        }
        catch (Exception ex)
        {
            throw new NetcapException(
                $"Failed to start HTTP listener on {prefix}: {ex.Message}", ex);
        }
        _listener = listener;
        _listenUrl = prefix;

        _writeStream = new FileStream(_filePath, FileMode.Create, FileAccess.Write,
            FileShare.Read);

        _startedAt = DateTimeOffset.UtcNow;
        _loopCts = new CancellationTokenSource();
        _loopTask = Task.Run(() => AcceptLoopAsync(_loopCts.Token), _loopCts.Token);

        Log.HttpSourceListening(_logger, prefix);
        return Task.CompletedTask;
    }

    private static int GetEphemeralPort()
    {
        var l = new System.Net.Sockets.TcpListener(IPAddress.Loopback, 0);
        try
        {
            l.Start();
            return ((IPEndPoint)l.LocalEndpoint).Port;
        }
        finally
        {
            l.Stop();
            l.Dispose();
        }
    }

    private async Task AcceptLoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested && _listener?.IsListening == true)
        {
            HttpListenerContext context;
            try
            {
                context = await _listener.GetContextAsync().ConfigureAwait(false);
            }
            catch (ObjectDisposedException) { break; }
            catch (HttpListenerException) { break; }

            _ = Task.Run(() => HandleAsync(context, ct), CancellationToken.None);

            if (Interlocked.Read(ref _packetCount) >= _maxPackets ||
                Interlocked.Read(ref _byteCount) >= _maxBytes ||
                DateTimeOffset.UtcNow - _startedAt >= _maxDuration)
            {
                try { _listener?.Stop(); } catch { /* ignore */ }
                break;
            }
        }
    }

    private async Task HandleAsync(HttpListenerContext context, CancellationToken ct)
    {
        try
        {
            var receivedAt = DateTimeOffset.UtcNow;
            long bodyBytes = 0;
            try
            {
                bodyBytes = await DrainAsync(context.Request.InputStream, ct)
                    .ConfigureAwait(false);
            }
            catch
            {
                // Tolerate body read failures.
            }
            var annotations = BuildAnnotations(context, receivedAt, bodyBytes);
            WriteEvent(annotations, receivedAt, bodyBytes);

            try
            {
                context.Response.StatusCode = (int)HttpStatusCode.NoContent;
                context.Response.ContentLength64 = 0;
                context.Response.Close();
            }
            catch
            {
                // Client may have disconnected.
            }
        }
        catch (Exception ex)
        {
            Log.HttpRequestHandlingError(_logger, ex);
        }
    }

    private static async Task<long> DrainAsync(Stream s, CancellationToken ct)
    {
        var total = 0L;
        var buf = new byte[8192];
        int n;
        while ((n = await s.ReadAsync(buf, ct).ConfigureAwait(false)) > 0)
        {
            total += n;
        }
        return total;
    }

    private static Dictionary<string, string?> BuildAnnotations(
        HttpListenerContext context, DateTimeOffset receivedAt, long bodyBytes)
    {
        var req = context.Request;
        var headers = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var h in req.Headers.AllKeys)
        {
            if (string.IsNullOrEmpty(h))
            {
                continue;
            }
            var raw = req.Headers[h];
            var value = string.Equals(h, "Authorization", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(h, "Cookie", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(h, "Set-Cookie", StringComparison.OrdinalIgnoreCase)
                ? "<redacted>"
                : raw ?? string.Empty;
            headers[h] = value;
        }
        return new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            ["method"] = req.HttpMethod,
            ["url"] = req.Url?.ToString(),
            ["rawUrl"] = req.RawUrl,
            ["protocol"] = req.ProtocolVersion.ToString(),
            ["userAgent"] = req.UserAgent,
            ["remoteEndpoint"] = req.RemoteEndPoint?.ToString(),
            ["receivedAt"] = receivedAt.ToString("O",
                System.Globalization.CultureInfo.InvariantCulture),
            ["bodyBytes"] = bodyBytes.ToString(
                System.Globalization.CultureInfo.InvariantCulture),
            ["headers"] = JsonSerializer.Serialize(headers)
        };
    }

    private void WriteEvent(Dictionary<string, string?> annotations,
        DateTimeOffset ts, long bodyBytes)
    {
        if (_writeStream is null)
        {
            return;
        }
        var payload = new
        {
            timestamp = ts,
            bodyBytes,
            annotations
        };
        var line = JsonSerializer.Serialize(payload) + "\n";
        var bytes = System.Text.Encoding.UTF8.GetBytes(line);
        lock (_writeLock)
        {
            _writeStream.Write(bytes);
            _writeStream.Flush();
        }
        Interlocked.Increment(ref _packetCount);
        Interlocked.Add(ref _byteCount, bytes.Length);
    }

    public async Task StopAsync(CancellationToken ct)
    {
        try { _listener?.Stop(); } catch { /* tolerate */ }
        try { _listener?.Close(); } catch { /* tolerate */ }
        _listener = null;

        if (_loopCts is not null)
        {
            try
            {
                await _loopCts.CancelAsync().ConfigureAwait(false);
            }
            catch
            {
                // Tolerate cancel-on-disposed.
            }
        }
        if (_loopTask is not null)
        {
            try
            {
                await _loopTask.WaitAsync(TimeSpan.FromSeconds(5), ct)
                    .ConfigureAwait(false);
            }
            catch { /* tolerate */ }
        }
        _loopCts?.Dispose();
        _loopCts = null;
        _loopTask = null;

        FileStream? stream;
        lock (_writeLock)
        {
            stream = _writeStream;
            _writeStream = null;
        }
        if (stream is not null)
        {
            try
            {
                await stream.FlushAsync(ct).ConfigureAwait(false);
            }
            catch { /* tolerate */ }
            await stream.DisposeAsync().ConfigureAwait(false);
        }
    }

    public async IAsyncEnumerable<CapturedPacket> ReadAllAsync(
        long? maxPackets,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct)
    {
        if (!File.Exists(_filePath))
        {
            yield break;
        }
        var limit = maxPackets ?? long.MaxValue;
        var count = 0L;
        await foreach (var line in File.ReadLinesAsync(_filePath, ct)
            .ConfigureAwait(false))
        {
            if (count >= limit)
            {
                yield break;
            }
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }
            CapturedPacket? packet = null;
            try
            {
                using var doc = JsonDocument.Parse(line);
                var ts = doc.RootElement.GetProperty("timestamp")
                    .GetDateTimeOffset();
                var bodyLen = doc.RootElement.TryGetProperty("bodyBytes",
                    out var b) ? b.GetInt64() : 0;
                var dict = new Dictionary<string, string?>(StringComparer.Ordinal);
                if (doc.RootElement.TryGetProperty("annotations", out var a))
                {
                    foreach (var prop in a.EnumerateObject())
                    {
                        dict[prop.Name] = prop.Value.ValueKind == JsonValueKind.Null
                            ? null
                            : prop.Value.ToString();
                    }
                }
                packet = new CapturedPacket(
                    Timestamp: ts,
                    OriginalLength: (int)bodyLen,
                    Data: ReadOnlyMemory<byte>.Empty,
                    LinkType: LinkLayers.Null,
                    Annotations: dict);
            }
            catch
            {
                // Skip malformed lines.
            }
            if (packet is not null)
            {
                yield return packet;
                count++;
            }
        }
    }

    public async ValueTask DisposeAsync()
    {
        try
        {
            await StopAsync(CancellationToken.None).ConfigureAwait(false);
        }
        catch
        {
            // Suppress on dispose.
        }
    }
}
