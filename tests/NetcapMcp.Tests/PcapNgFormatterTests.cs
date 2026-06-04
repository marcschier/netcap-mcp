// Licensed under the Apache License, Version 2.0.

namespace NetcapMcp.Tests;

using System.Buffers.Binary;
using System.Collections.Generic;
using Microsoft.Extensions.Logging.Abstractions;
using NetcapMcp.Capture;
using NetcapMcp.Formats;
using NetcapMcp.Models;
using PacketDotNet;
using Xunit;

public sealed class PcapNgFormatterTests
{
    private const uint kBlockTypeShb = 0x0A0D0D0AU;
    private const uint kBlockTypeIdb = 0x00000001U;
    private const uint kBlockTypeEpb = 0x00000006U;
    private const uint kByteOrderMagic = 0x1A2B3C4DU;

    [Fact]
    public async Task FormatAsync_WritesShbIdbAndEpbsInOrder()
    {
        var packets = new[]
        {
            MakePacket(LinkLayers.Ethernet, [0x10, 0x20, 0x30, 0x40]),
            MakePacket(LinkLayers.Ethernet, [0x55, 0xAA, 0x55, 0xAA, 0xFF])
        };
        var session = MakeSession(packets, LinkLayers.Ethernet);

        var formatter = new PcapNgFormatter();
        var result = await formatter.FormatAsync(session, maxPackets: null,
            CancellationToken.None);

        Assert.True(result.IsBinary);
        Assert.NotNull(result.Bytes);
        Assert.Equal(2, result.PacketsFormatted);
        Assert.Equal("application/x-pcapng", result.MimeType);

        var blocks = ParseBlocks(result.Bytes!);
        Assert.Equal(4, blocks.Count); // SHB + IDB + 2 EPB
        Assert.Equal(kBlockTypeShb, blocks[0].BlockType);
        Assert.Equal(kBlockTypeIdb, blocks[1].BlockType);
        Assert.Equal(kBlockTypeEpb, blocks[2].BlockType);
        Assert.Equal(kBlockTypeEpb, blocks[3].BlockType);

        // SHB byte-order magic at offset 8.
        Assert.Equal(kByteOrderMagic,
            BinaryPrimitives.ReadUInt32LittleEndian(result.Bytes.AsSpan(8, 4)));
    }

    [Fact]
    public async Task FormatAsync_RespectsMaxPackets()
    {
        var packets = Enumerable.Range(0, 5)
            .Select(i => MakePacket(LinkLayers.Ethernet, [(byte)i, 0, 0, 0]))
            .ToArray();
        var session = MakeSession(packets, LinkLayers.Ethernet);

        var formatter = new PcapNgFormatter();
        var result = await formatter.FormatAsync(session, maxPackets: 3,
            CancellationToken.None);

        Assert.Equal(3, result.PacketsFormatted);
        var blocks = ParseBlocks(result.Bytes!);
        Assert.Equal(5, blocks.Count); // SHB + IDB + 3 EPB
    }

    [Fact]
    public async Task FormatAsync_ThrowsOnNullLinkType()
    {
        var session = MakeSession([], LinkLayers.Null);
        var formatter = new PcapNgFormatter();

        var ex = await Assert.ThrowsAsync<NetcapMcpException>(
            () => formatter.FormatAsync(session, null, CancellationToken.None));
        Assert.Contains("does not produce link-layer", ex.Message,
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task FormatAsync_PadsPacketDataToFourByteBoundary()
    {
        // 5-byte packet -> padded with 3 zero bytes.
        var packet = MakePacket(LinkLayers.Ethernet, [1, 2, 3, 4, 5]);
        var session = MakeSession([packet], LinkLayers.Ethernet);

        var formatter = new PcapNgFormatter();
        var result = await formatter.FormatAsync(session, null,
            CancellationToken.None);

        var blocks = ParseBlocks(result.Bytes!);
        var epb = blocks.Last(b => b.BlockType == kBlockTypeEpb);
        // EPB body: ifaceId(4) + tsHigh(4) + tsLow(4) + capLen(4) + origLen(4)
        // + packetData(5) + pad(3) -> 24 + 8 = 32 bytes body
        // total = 8 header + 24 body + 8 trailer? wait header(8)+body(24+5+3)+trailer(4)
        // Just verify total block length matches.
        var bodyLen = 4 + 4 + 4 + 4 + 4 + 5 + 3;
        Assert.Equal((uint)(8 + bodyLen + 4), epb.TotalLength);
    }

    private static CapturedPacket MakePacket(LinkLayers link, byte[] data)
        => new(DateTimeOffset.UnixEpoch.AddSeconds(1), data.Length, data,
            link, kEmpty);

    private static CaptureSession MakeSession(IEnumerable<CapturedPacket> packets,
        LinkLayers link)
    {
        var source = new FakeCaptureSource(packets, linkType: link);
        return new CaptureSession(
            id: Guid.NewGuid().ToString("N"),
            sourceName: "fake",
            source: source,
            sessionFolder: Path.GetTempPath(),
            request: new StartCaptureRequest { Source = "fake" },
            logger: NullLogger.Instance);
    }

    private sealed record BlockHeader(uint BlockType, uint TotalLength,
        int Offset);

    private static List<BlockHeader> ParseBlocks(byte[] bytes)
    {
        var blocks = new List<BlockHeader>();
        var offset = 0;
        while (offset + 8 <= bytes.Length)
        {
            var type = BinaryPrimitives.ReadUInt32LittleEndian(
                bytes.AsSpan(offset, 4));
            var length = BinaryPrimitives.ReadUInt32LittleEndian(
                bytes.AsSpan(offset + 4, 4));
            blocks.Add(new BlockHeader(type, length, offset));
            if (length < 12 || offset + (int)length > bytes.Length)
            {
                break;
            }
            offset += (int)length;
        }
        return blocks;
    }

    private static readonly IReadOnlyDictionary<string, string?> kEmpty =
        new Dictionary<string, string?>();
}
