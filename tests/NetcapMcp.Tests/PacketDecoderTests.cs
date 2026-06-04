// Licensed under the Apache License, Version 2.0.

namespace NetcapMcp.Tests;

using System.Collections.Generic;
using System.Net;
using NetcapMcp.Capture;
using NetcapMcp.Formats;
using NetcapMcp.Models;
using PacketDotNet;
using Xunit;

public sealed class PacketDecoderTests
{
    [Fact]
    public void Decode_EthernetIpv4Tcp_ReturnsExpectedFields()
    {
        var bytes = BuildEthernetIpv4Tcp(
            srcMac: "01:02:03:04:05:06",
            dstMac: "0a:0b:0c:0d:0e:0f",
            srcIp: IPAddress.Parse("10.0.0.1"),
            dstIp: IPAddress.Parse("10.0.0.2"),
            srcPort: 12345,
            dstPort: 80,
            payload: [0xAA, 0xBB, 0xCC]);
        var captured = new CapturedPacket(
            Timestamp: DateTimeOffset.UnixEpoch.AddSeconds(1),
            OriginalLength: bytes.Length,
            Data: bytes,
            LinkType: LinkLayers.Ethernet,
            Annotations: kEmpty);

        var decoded = PacketDecoder.Decode(captured);

        Assert.Equal("10.0.0.1", decoded.Source);
        Assert.Equal("10.0.0.2", decoded.Destination);
        Assert.Equal("tcp", decoded.Protocol);
        Assert.Equal(12345, decoded.SourcePort);
        Assert.Equal(80, decoded.DestinationPort);
        Assert.Equal(3, decoded.PayloadLength);
        Assert.Equal(bytes.Length, decoded.Length);
    }

    [Fact]
    public void Decode_EthernetIpv4Udp_ReturnsExpectedFields()
    {
        var bytes = BuildEthernetIpv4Udp(
            srcIp: IPAddress.Parse("192.168.1.1"),
            dstIp: IPAddress.Parse("192.168.1.255"),
            srcPort: 5353,
            dstPort: 5353,
            payload: [0x00, 0x01, 0x02, 0x03]);
        var captured = new CapturedPacket(
            DateTimeOffset.UtcNow, bytes.Length, bytes,
            LinkLayers.Ethernet, kEmpty);

        var decoded = PacketDecoder.Decode(captured);

        Assert.Equal("192.168.1.1", decoded.Source);
        Assert.Equal("192.168.1.255", decoded.Destination);
        Assert.Equal("udp", decoded.Protocol);
        Assert.Equal(5353, decoded.SourcePort);
        Assert.Equal(5353, decoded.DestinationPort);
    }

    [Fact]
    public void Decode_HttpSourcePacket_ProjectsAnnotations()
    {
        var annotations = new Dictionary<string, string?>
        {
            ["method"] = "GET",
            ["url"] = "http://example.com/x",
            ["remoteEndpoint"] = "127.0.0.1:12345"
        };
        var captured = new CapturedPacket(
            DateTimeOffset.UtcNow, 42, ReadOnlyMemory<byte>.Empty,
            LinkLayers.Null, annotations);

        var decoded = PacketDecoder.Decode(captured);

        Assert.Equal("http", decoded.Protocol);
        Assert.Equal("127.0.0.1:12345", decoded.Source);
        Assert.Equal("http://example.com/x", decoded.Destination);
        Assert.Equal(42, decoded.Length);
        Assert.NotNull(decoded.Annotations);
    }

    [Fact]
    public void Decode_CorruptedFrame_DoesNotThrow()
    {
        var bytes = new byte[] { 0xDE, 0xAD, 0xBE, 0xEF };
        var captured = new CapturedPacket(DateTimeOffset.UtcNow, bytes.Length,
            bytes, LinkLayers.Ethernet, kEmpty);

        var decoded = PacketDecoder.Decode(captured);

        Assert.Equal(bytes.Length, decoded.Length);
    }

    private static byte[] BuildEthernetIpv4Tcp(string srcMac, string dstMac,
        IPAddress srcIp, IPAddress dstIp, ushort srcPort, ushort dstPort,
        byte[] payload)
    {
        var tcp = new TcpPacket(srcPort, dstPort)
        {
            PayloadData = payload,
            WindowSize = 8192,
            SequenceNumber = 1,
            AcknowledgmentNumber = 0,
            DataOffset = 5
        };
        var ip = new IPv4Packet(srcIp, dstIp)
        {
            PayloadPacket = tcp,
            Protocol = ProtocolType.Tcp,
            TimeToLive = 64
        };
        tcp.UpdateCalculatedValues();
        ip.UpdateCalculatedValues();
        var eth = new EthernetPacket(
            System.Net.NetworkInformation.PhysicalAddress.Parse(srcMac.Replace(':', '-')),
            System.Net.NetworkInformation.PhysicalAddress.Parse(dstMac.Replace(':', '-')),
            EthernetType.IPv4)
        {
            PayloadPacket = ip
        };
        return eth.Bytes;
    }

    private static byte[] BuildEthernetIpv4Udp(IPAddress srcIp, IPAddress dstIp,
        ushort srcPort, ushort dstPort, byte[] payload)
    {
        var udp = new UdpPacket(srcPort, dstPort) { PayloadData = payload };
        var ip = new IPv4Packet(srcIp, dstIp)
        {
            PayloadPacket = udp,
            Protocol = ProtocolType.Udp,
            TimeToLive = 64
        };
        udp.UpdateCalculatedValues();
        ip.UpdateCalculatedValues();
        var eth = new EthernetPacket(
            System.Net.NetworkInformation.PhysicalAddress.Parse("00-11-22-33-44-55"),
            System.Net.NetworkInformation.PhysicalAddress.Parse("66-77-88-99-AA-BB"),
            EthernetType.IPv4)
        {
            PayloadPacket = ip
        };
        return eth.Bytes;
    }

    private static readonly IReadOnlyDictionary<string, string?> kEmpty =
        new Dictionary<string, string?>();
}
