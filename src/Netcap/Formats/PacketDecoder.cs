// Licensed under the Apache License, Version 2.0.

namespace Netcap.Formats;

using System.Net;
using Netcap.Capture;
using PacketDotNet;

/// <summary>
/// Normalized representation of a captured record, suitable for
/// JSON/CSV/Text output and summarization. Decoded from
/// <see cref="CapturedPacket"/> via <see cref="PacketDecoder"/>.
/// </summary>
internal sealed record class DecodedPacket
{
    public required DateTimeOffset Timestamp { get; init; }
    public required int Length { get; init; }
    public string? Source { get; init; }
    public string? Destination { get; init; }
    public string? Protocol { get; init; }
    public int? SourcePort { get; init; }
    public int? DestinationPort { get; init; }
    public int? PayloadLength { get; init; }
    public IReadOnlyDictionary<string, string?>? Annotations { get; init; }
}

internal static class PacketDecoder
{
    /// <summary>
    /// Decode a captured packet using <see cref="Packet.ParsePacket"/> when
    /// link-layer data is present, otherwise project the source's
    /// annotations directly.
    /// </summary>
    public static DecodedPacket Decode(CapturedPacket packet)
    {
        if (packet.LinkType == LinkLayers.Null || packet.Data.IsEmpty)
        {
            return new DecodedPacket
            {
                Timestamp = packet.Timestamp,
                Length = packet.OriginalLength,
                Source = packet.Annotations.TryGetValue("remoteEndpoint", out var src)
                    ? src : null,
                Destination = packet.Annotations.TryGetValue("url", out var dst)
                    ? dst : null,
                Protocol = "http",
                PayloadLength = packet.OriginalLength,
                Annotations = packet.Annotations
            };
        }

        try
        {
            var parsed = Packet.ParsePacket(packet.LinkType, packet.Data.ToArray());
            return FromParsed(parsed, packet.Timestamp, packet.OriginalLength);
        }
        catch
        {
            return new DecodedPacket
            {
                Timestamp = packet.Timestamp,
                Length = packet.OriginalLength,
                Protocol = packet.LinkType.ToString()
            };
        }
    }

    private static DecodedPacket FromParsed(Packet parsed, DateTimeOffset ts,
        int originalLength)
    {
        string? src = null, dst = null, proto = null;
        int? srcPort = null, dstPort = null, payload = null;

        var ipv4 = parsed.Extract<IPv4Packet>();
        if (ipv4 is not null)
        {
            src = ipv4.SourceAddress?.ToString();
            dst = ipv4.DestinationAddress?.ToString();
            proto = ipv4.Protocol.ToString();
        }
        else
        {
            var ipv6 = parsed.Extract<IPv6Packet>();
            if (ipv6 is not null)
            {
                src = ipv6.SourceAddress?.ToString();
                dst = ipv6.DestinationAddress?.ToString();
                proto = ipv6.NextHeader.ToString();
            }
        }

        var tcp = parsed.Extract<TcpPacket>();
        if (tcp is not null)
        {
            srcPort = tcp.SourcePort;
            dstPort = tcp.DestinationPort;
            proto = "tcp";
            payload = tcp.PayloadData?.Length;
        }
        else
        {
            var udp = parsed.Extract<UdpPacket>();
            if (udp is not null)
            {
                srcPort = udp.SourcePort;
                dstPort = udp.DestinationPort;
                proto = "udp";
                payload = udp.PayloadData?.Length;
            }
        }

        if (proto is null)
        {
            var arp = parsed.Extract<ArpPacket>();
            if (arp is not null)
            {
                proto = "arp";
                src = arp.SenderProtocolAddress?.ToString();
                dst = arp.TargetProtocolAddress?.ToString();
            }
        }

        if (src is null || dst is null)
        {
            var eth = parsed.Extract<EthernetPacket>();
            if (eth is not null)
            {
                src ??= FormatMac(eth.SourceHardwareAddress);
                dst ??= FormatMac(eth.DestinationHardwareAddress);
                proto ??= eth.Type.ToString();
            }
        }

        return new DecodedPacket
        {
            Timestamp = ts,
            Length = originalLength,
            Source = src,
            Destination = dst,
            Protocol = proto,
            SourcePort = srcPort,
            DestinationPort = dstPort,
            PayloadLength = payload
        };
    }

    private static string? FormatMac(System.Net.NetworkInformation.PhysicalAddress? addr)
    {
        if (addr is null)
        {
            return null;
        }
        var bytes = addr.GetAddressBytes();
        return bytes.Length == 0
            ? null
            : string.Join(':', bytes.Select(b => b.ToString("x2",
                System.Globalization.CultureInfo.InvariantCulture)));
    }
}
