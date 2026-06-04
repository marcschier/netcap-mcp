// Licensed under the Apache License, Version 2.0.

namespace NetcapMcp.Formats;

using NetcapMcp.Capture;
using NetcapMcp.Models;

/// <summary>
/// Pass-through formatter for the libpcap file produced by sources that
/// keep a raw pcap on disk. Rejects sources without one.
/// </summary>
internal sealed class PcapFormatter : ITraceFormatter
{
    public FormatKind Kind => FormatKind.Pcap;
    public string MimeType => "application/vnd.tcpdump.pcap";

    public async Task<FormatResult> FormatAsync(CaptureSession session,
        long? maxPackets, CancellationToken ct)
    {
        var path = session.Source.GetRawPcapFilePath();
        if (path is null || !File.Exists(path))
        {
            throw new NetcapMcpException(
                $"Source '{session.SourceName}' does not produce a libpcap file. " +
                "Try format=json, csv or text.");
        }
        var bytes = await File.ReadAllBytesAsync(path, ct).ConfigureAwait(false);
        return new FormatResult
        {
            Kind = Kind,
            MimeType = MimeType,
            Bytes = bytes,
            PacketsFormatted = session.Source.PacketCount
        };
    }
}
