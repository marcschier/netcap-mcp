// Licensed under the Apache License, Version 2.0.

namespace NetcapMcp.Tests;

using NetcapMcp.Capture;
using NetcapMcp.Models;
using PacketDotNet;

/// <summary>
/// In-memory <see cref="ICaptureSource"/> for tests. The list of packets
/// is supplied at construction; Start/Stop are no-ops and ReadAllAsync
/// just iterates the seeded list.
/// </summary>
internal sealed class FakeCaptureSource : ICaptureSource
{
    private readonly List<CapturedPacket> _packets;
    private readonly string? _rawPath;

    public FakeCaptureSource(IEnumerable<CapturedPacket> packets,
        LinkLayers linkType = LinkLayers.Ethernet,
        IReadOnlySet<FormatKind>? supported = null,
        string? rawPcapFilePath = null)
    {
        _packets = [.. packets];
        LinkType = linkType;
        SupportedFormats = supported ?? new HashSet<FormatKind>
        {
            FormatKind.Pcap, FormatKind.PcapNg, FormatKind.Json,
            FormatKind.Csv, FormatKind.Text
        };
        _rawPath = rawPcapFilePath;
        PacketCount = _packets.Count;
        ByteCount = _packets.Sum(p => (long)p.OriginalLength);
    }

    public LinkLayers LinkType { get; }
    public IReadOnlySet<FormatKind> SupportedFormats { get; }
    public long PacketCount { get; }
    public long ByteCount { get; }
    public string? ListenUrl => null;

    public bool StartCalled { get; private set; }
    public bool StopCalled { get; private set; }
    public int DisposeCount { get; private set; }

    public string? GetRawPcapFilePath() => _rawPath;

    public Task StartAsync(StartCaptureRequest request, CancellationToken ct)
    {
        StartCalled = true;
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken ct)
    {
        StopCalled = true;
        return Task.CompletedTask;
    }

#pragma warning disable CS1998 // async without await: yield-only IAsyncEnumerable
    public async IAsyncEnumerable<CapturedPacket> ReadAllAsync(long? maxPackets,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct)
#pragma warning restore CS1998
    {
        var limit = maxPackets ?? long.MaxValue;
        var taken = 0L;
        foreach (var p in _packets)
        {
            if (taken >= limit)
            {
                yield break;
            }
            ct.ThrowIfCancellationRequested();
            yield return p;
            taken++;
        }
    }

    public ValueTask DisposeAsync()
    {
        DisposeCount++;
        return ValueTask.CompletedTask;
    }
}
