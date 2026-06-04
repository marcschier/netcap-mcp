// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace NetcapMcp.Capture;

using System.Threading.Channels;
using Microsoft.Extensions.Logging;
using NetcapMcp.Models;
using PacketDotNet;
using SharpPcap;
using SharpPcap.LibPcap;

/// <summary>
/// Single-interface SharpPcap-based capture source. Persists raw frames to
/// a libpcap-format file under the session folder.
/// </summary>
internal sealed class PcapCaptureSource : ICaptureSource
{
    private const int kDefaultMaxBytes = 50 * 1024 * 1024;
    private const int kDefaultMaxDurationSeconds = 30 * 60;
    private const string kPcapFileName = "capture.pcap";

    private readonly ILogger _logger;
    private readonly string _folder;
    private readonly string _filePath;
    private readonly Lock _lock = new();

    private LibPcapLiveDevice? _device;
    private CaptureFileWriterDevice? _writer;
    private long _packetCount;
    private long _byteCount;
    private long _maxBytes;
    private long _maxPackets;
    private DateTimeOffset _startedAt;
    private TimeSpan _maxDuration;
    private bool _stopRequested;

    public PcapCaptureSource(string sessionFolder, ILogger logger)
    {
        _folder = sessionFolder;
        _filePath = Path.Combine(sessionFolder, kPcapFileName);
        _logger = logger;
    }

    public LinkLayers LinkType { get; private set; } = LinkLayers.Null;

    public IReadOnlySet<FormatKind> SupportedFormats { get; } =
        new HashSet<FormatKind>
        {
            FormatKind.Pcap, FormatKind.PcapNg, FormatKind.Json,
            FormatKind.Csv, FormatKind.Text
        };

    public long PacketCount => Interlocked.Read(ref _packetCount);

    public long ByteCount => Interlocked.Read(ref _byteCount);

    public string? ListenUrl => null;

    public string? GetRawPcapFilePath()
        => File.Exists(_filePath) ? _filePath : null;

    public Task StartAsync(StartCaptureRequest request, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(request.InterfaceName))
        {
            throw new NetcapMcpException(
                "pcap source requires 'interfaceName'. Use list_interfaces to discover.");
        }

        _maxBytes = request.MaxBytes ?? kDefaultMaxBytes;
        _maxPackets = request.MaxPackets ?? long.MaxValue;
        var duration = request.MaxDurationSeconds ?? kDefaultMaxDurationSeconds;
        _maxDuration = TimeSpan.FromSeconds(duration);

        var devices = LibPcapLiveDeviceList.New();
        LibPcapLiveDevice? selected = null;
        foreach (var d in devices)
        {
            if (string.Equals(d.Name, request.InterfaceName, StringComparison.Ordinal) ||
                string.Equals(d.Description, request.InterfaceName, StringComparison.Ordinal))
            {
                selected = d;
            }
            else
            {
                d.Dispose();
            }
        }
        if (selected is null)
        {
            throw new NetcapMcpException(
                $"Interface '{request.InterfaceName}' not found. Use list_interfaces.");
        }

        var promiscuous = request.Promiscuous ?? true;
        try
        {
            selected.Open(mode: promiscuous ? DeviceModes.Promiscuous : DeviceModes.None,
                read_timeout: 1000);
        }
        catch
        {
            if (!promiscuous)
            {
                throw;
            }
            if (_logger.IsEnabled(LogLevel.Information))
            {
                _logger.LogInformation(
                    "Failed promiscuous open on {Device}, falling back to normal mode.",
                    selected.Name);
            }
            selected.Open(mode: DeviceModes.None, read_timeout: 1000);
        }

        if (!string.IsNullOrEmpty(request.BpfFilter))
        {
            selected.Filter = request.BpfFilter;
        }

        LinkType = selected.LinkType;
        _writer = new CaptureFileWriterDevice(_filePath, FileMode.Create);
        _writer.Open(new DeviceConfiguration { LinkLayerType = LinkType });
        selected.OnPacketArrival += OnPacketArrival;
        _device = selected;
        _startedAt = DateTimeOffset.UtcNow;

        if (_logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation(
                "pcap source capturing on {Device} ({LinkType}) filter={Filter}",
                selected.Name, LinkType, request.BpfFilter ?? "<none>");
        }

#pragma warning disable CA1849 // StartCapture is the SharpPcap blocking API.
        selected.StartCapture();
#pragma warning restore CA1849
        return Task.CompletedTask;
    }

    private void OnPacketArrival(object sender, PacketCapture e)
    {
        if (_stopRequested)
        {
            return;
        }
        var pkt = e.GetPacket();
        var len = pkt.PacketLength;
        var packets = Interlocked.Increment(ref _packetCount);
        var bytes = Interlocked.Add(ref _byteCount, len);

        lock (_lock)
        {
            _writer?.Write(pkt);
        }

        if (bytes >= _maxBytes ||
            packets >= _maxPackets ||
            DateTimeOffset.UtcNow - _startedAt >= _maxDuration)
        {
            // Stop the device asynchronously to avoid blocking the capture
            // thread. Lock-protected StopAsync is idempotent.
            _stopRequested = true;
            _ = Task.Run(() =>
            {
                try
                {
                    _device?.StopCapture();
                }
                catch
                {
                    // Suppress; final shutdown is done in StopAsync.
                }
            });
        }
    }

    public Task StopAsync(CancellationToken ct)
    {
        lock (_lock)
        {
            if (_device != null)
            {
                try { _device.StopCapture(); } catch { /* tolerate already-stopped */ }
                _device.OnPacketArrival -= OnPacketArrival;
                _device.Dispose();
                _device = null;
            }
            if (_writer != null)
            {
                try { _writer.Close(); } catch { /* tolerate already-closed */ }
                _writer.Dispose();
                _writer = null;
            }
        }
        return Task.CompletedTask;
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
        using var reader = new CaptureFileReaderDevice(_filePath);
        reader.Open();
        while (count < limit && !ct.IsCancellationRequested)
        {
            var status = reader.GetNextPacket(out var packetEvent);
            if (status != GetPacketStatus.PacketRead)
            {
                break;
            }
            var raw = packetEvent.GetPacket();
            var ts = raw.Timeval.Date;
            var data = raw.Data.ToArray();
            yield return new CapturedPacket(
                Timestamp: new DateTimeOffset(ts, TimeSpan.Zero),
                OriginalLength: raw.PacketLength,
                Data: data,
                LinkType: LinkType,
                Annotations: kEmpty);
            count++;
            if ((count & 0xFF) == 0)
            {
                await Task.Yield();
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

    private static readonly IReadOnlyDictionary<string, string?> kEmpty =
        new Dictionary<string, string?>();
}
