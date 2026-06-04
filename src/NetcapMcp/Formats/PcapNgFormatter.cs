// Licensed under the Apache License, Version 2.0.

namespace NetcapMcp.Formats;

using System.Buffers.Binary;
using NetcapMcp.Capture;
using NetcapMcp.Models;
using PacketDotNet;

/// <summary>
/// Minimal pcapng writer following IETF draft-tuexen-opsawg-pcapng. Writes
/// a Section Header Block, one Interface Description Block, then one
/// Enhanced Packet Block per captured packet. Only used for sources that
/// produce link-layer frames (i.e. <see cref="LinkLayers.Null"/> is
/// rejected).
/// </summary>
internal sealed class PcapNgFormatter : ITraceFormatter
{
    private const uint kBlockTypeShb = 0x0A0D0D0AU;
    private const uint kBlockTypeIdb = 0x00000001U;
    private const uint kBlockTypeEpb = 0x00000006U;
    private const uint kByteOrderMagic = 0x1A2B3C4DU;

    public FormatKind Kind => FormatKind.PcapNg;
    public string MimeType => "application/x-pcapng";

    public async Task<FormatResult> FormatAsync(CaptureSession session,
        long? maxPackets, CancellationToken ct)
    {
        if (session.Source.LinkType == LinkLayers.Null)
        {
            throw new NetcapMcpException(
                $"Source '{session.SourceName}' does not produce link-layer " +
                "frames; pcapng is not applicable. Use json, csv or text.");
        }

        using var ms = new MemoryStream();
        WriteShb(ms);
        WriteIdb(ms, session.Source.LinkType);

        var count = 0L;
        var startTicks = DateTimeOffset.UnixEpoch.UtcTicks;
        await foreach (var packet in session.Source
            .ReadAllAsync(maxPackets, ct).ConfigureAwait(false))
        {
            ct.ThrowIfCancellationRequested();
            // Pcapng timestamp: 64-bit microsecond count since epoch.
            var us = (ulong)((packet.Timestamp.UtcTicks - startTicks) / 10);
            WriteEpb(ms, us, packet.Data.Span, packet.OriginalLength);
            count++;
        }

        return new FormatResult
        {
            Kind = Kind,
            MimeType = MimeType,
            Bytes = ms.ToArray(),
            PacketsFormatted = count
        };
    }

    private static void WriteShb(Stream s)
    {
        // SHB body: byteOrderMagic(4) + majorVersion(2) + minorVersion(2) + sectionLength(8)
        const int bodyLen = 4 + 2 + 2 + 8;
        var totalLen = 8 + bodyLen + 4;
        WriteUInt32(s, kBlockTypeShb);
        WriteUInt32(s, (uint)totalLen);
        WriteUInt32(s, kByteOrderMagic);
        WriteUInt16(s, 1);
        WriteUInt16(s, 0);
        WriteInt64(s, -1L);
        WriteUInt32(s, (uint)totalLen);
    }

    private static void WriteIdb(Stream s, LinkLayers linkType)
    {
        // IDB body: linkType(2) + reserved(2) + snapLen(4)
        const int bodyLen = 2 + 2 + 4;
        var totalLen = 8 + bodyLen + 4;
        WriteUInt32(s, kBlockTypeIdb);
        WriteUInt32(s, (uint)totalLen);
        WriteUInt16(s, (ushort)linkType);
        WriteUInt16(s, 0);
        WriteUInt32(s, 0xFFFFU);
        WriteUInt32(s, (uint)totalLen);
    }

    private static void WriteEpb(Stream s, ulong timestampMicros,
        ReadOnlySpan<byte> data, int originalLength)
    {
        var pad = (4 - (data.Length & 3)) & 3;
        // EPB body: ifaceId(4) + tsHigh(4) + tsLow(4) + capLen(4) + origLen(4)
        //           + packetData(N + pad)
        var bodyLen = 4 + 4 + 4 + 4 + 4 + data.Length + pad;
        var totalLen = 8 + bodyLen + 4;
        WriteUInt32(s, kBlockTypeEpb);
        WriteUInt32(s, (uint)totalLen);
        WriteUInt32(s, 0);
        WriteUInt32(s, (uint)(timestampMicros >> 32));
        WriteUInt32(s, (uint)(timestampMicros & 0xFFFFFFFFU));
        WriteUInt32(s, (uint)data.Length);
        WriteUInt32(s, (uint)originalLength);
        s.Write(data);
        for (var i = 0; i < pad; i++)
        {
            s.WriteByte(0);
        }
        WriteUInt32(s, (uint)totalLen);
    }

    private static void WriteUInt16(Stream s, ushort value)
    {
        Span<byte> buf = stackalloc byte[2];
        BinaryPrimitives.WriteUInt16LittleEndian(buf, value);
        s.Write(buf);
    }

    private static void WriteUInt32(Stream s, uint value)
    {
        Span<byte> buf = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32LittleEndian(buf, value);
        s.Write(buf);
    }

    private static void WriteInt64(Stream s, long value)
    {
        Span<byte> buf = stackalloc byte[8];
        BinaryPrimitives.WriteInt64LittleEndian(buf, value);
        s.Write(buf);
    }
}
