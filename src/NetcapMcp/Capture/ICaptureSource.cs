// Licensed under the Apache License, Version 2.0.

namespace NetcapMcp.Capture;

using NetcapMcp.Models;
using PacketDotNet;

/// <summary>
/// Abstraction for a network trace source. Implementations buffer packets
/// to disk so completed captures can be replayed by formatters.
/// </summary>
internal interface ICaptureSource : IAsyncDisposable
{
    /// <summary>
    /// The link-layer type that <see cref="ReadAllAsync"/> packets carry,
    /// or <see cref="LinkLayers.Null"/> for sources that do not produce
    /// link-layer frames (e.g. HTTP).
    /// </summary>
    LinkLayers LinkType { get; }

    /// <summary>Output formats this source supports.</summary>
    IReadOnlySet<FormatKind> SupportedFormats { get; }

    /// <summary>Captured packet count (live).</summary>
    long PacketCount { get; }

    /// <summary>Captured byte count (live).</summary>
    long ByteCount { get; }

    /// <summary>
    /// Source-supplied connection or listener info exposed in
    /// <see cref="CaptureSessionInfo"/> (e.g. listen URL for HTTP source).
    /// May be <c>null</c>.
    /// </summary>
    string? ListenUrl { get; }

    /// <summary>Begin capturing.</summary>
    Task StartAsync(StartCaptureRequest request, CancellationToken ct);

    /// <summary>
    /// Stop capturing and flush. After this returns successfully the
    /// captured records are safe to enumerate via <see cref="ReadAllAsync"/>.
    /// </summary>
    Task StopAsync(CancellationToken ct);

    /// <summary>
    /// Replay all captured records from buffered storage. May be called
    /// multiple times after <see cref="StopAsync"/> completes.
    /// </summary>
    IAsyncEnumerable<CapturedPacket> ReadAllAsync(long? maxPackets, CancellationToken ct);

    /// <summary>
    /// Returns the full path to the underlying raw libpcap-format file if
    /// the source produces one (pcap source). <c>null</c> otherwise.
    /// </summary>
    string? GetRawPcapFilePath();
}
