// Licensed under the Apache License, Version 2.0.

namespace Netcap.Tests;

using System.Collections.Generic;
using Microsoft.Extensions.Logging.Abstractions;
using ModelContextProtocol.Protocol;
using Moq;
using Netcap.Capture;
using Netcap.Formats;
using Netcap.Mcp;
using Netcap.Models;
using PacketDotNet;
using Xunit;

public sealed class CaptureToolsTests
{
    [Fact]
    public async Task StartCaptureAsync_ThrowsWhenPcapMissingInterface()
    {
        var manager = NewManager();
        var ex = await Assert.ThrowsAsync<NetcapException>(() =>
            CaptureTools.StartCaptureAsync(manager,
                new StartCaptureRequest { Source = "pcap" },
                CancellationToken.None));
        Assert.Contains("interfaceName", ex.Message,
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task StartCaptureAsync_ThrowsForUnknownSource()
    {
        var manager = NewManager();
        var ex = await Assert.ThrowsAsync<NetcapException>(() =>
            CaptureTools.StartCaptureAsync(manager,
                new StartCaptureRequest { Source = "telnet" },
                CancellationToken.None));
        Assert.Contains("unknown source", ex.Message,
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task StartCaptureAsync_ThrowsForOutOfRangePort()
    {
        var manager = NewManager();
        var ex = await Assert.ThrowsAsync<NetcapException>(() =>
            CaptureTools.StartCaptureAsync(manager,
                new StartCaptureRequest { Source = "http", ListenPort = -1 },
                CancellationToken.None));
        Assert.Contains("listenPort", ex.Message,
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GetCaptureAsync_TextFormat_ReturnsTextContentBlocks()
    {
        var (manager, session) = await PrepareSessionWithFakeAsync(
            sourceName: "http",
            link: LinkLayers.Null,
            supported: new HashSet<FormatKind>
                { FormatKind.Text, FormatKind.Json, FormatKind.Csv });
        var formatters = NewFormatterRegistry();

        var blocks = await CaptureTools.GetCaptureAsync(manager, formatters,
            session.Id, format: "text", maxPackets: null,
            allowPartial: false, CancellationToken.None);

        Assert.Equal(2, blocks.Count);
        Assert.All(blocks, b => Assert.IsType<TextContentBlock>(b));
    }

    [Fact]
    public async Task GetCaptureAsync_PcapFormat_ReturnsEmbeddedResourceBlock()
    {
        var pcapBytes = MakeMinimalPcapFile();
        var rawPath = Path.Combine(Path.GetTempPath(),
            "netcapmcp-test-" + Path.GetRandomFileName() + ".pcap");
        await File.WriteAllBytesAsync(rawPath, pcapBytes);

        var (manager, session) = await PrepareSessionWithFakeAsync(
            sourceName: "pcap",
            link: LinkLayers.Ethernet,
            supported: new HashSet<FormatKind>
                { FormatKind.Pcap, FormatKind.Json },
            rawPcapFilePath: rawPath);
        var formatters = NewFormatterRegistry();

        var blocks = await CaptureTools.GetCaptureAsync(manager, formatters,
            session.Id, format: "pcap", maxPackets: null,
            allowPartial: false, CancellationToken.None);

        Assert.Equal(2, blocks.Count);
        Assert.IsType<TextContentBlock>(blocks[0]);
        var embedded = Assert.IsType<EmbeddedResourceBlock>(blocks[1]);
        var blob = Assert.IsType<BlobResourceContents>(embedded.Resource);
        Assert.Equal("application/vnd.tcpdump.pcap", blob.MimeType);

        try { File.Delete(rawPath); } catch { /* best effort */ }
    }

    [Fact]
    public async Task GetCaptureAsync_ThrowsWhenFormatNotSupportedBySource()
    {
        var (manager, session) = await PrepareSessionWithFakeAsync(
            sourceName: "http",
            link: LinkLayers.Null,
            supported: new HashSet<FormatKind>
                { FormatKind.Json, FormatKind.Csv, FormatKind.Text });
        var formatters = NewFormatterRegistry();

        await Assert.ThrowsAsync<NetcapException>(() =>
            CaptureTools.GetCaptureAsync(manager, formatters,
                session.Id, format: "pcap", maxPackets: null,
                allowPartial: false, CancellationToken.None));
    }

    [Fact]
    public async Task GetCaptureAsync_ThrowsForUnknownFormat()
    {
        var (manager, session) = await PrepareSessionWithFakeAsync(
            sourceName: "http",
            link: LinkLayers.Null,
            supported: new HashSet<FormatKind> { FormatKind.Json });
        var formatters = NewFormatterRegistry();

        await Assert.ThrowsAsync<NetcapException>(() =>
            CaptureTools.GetCaptureAsync(manager, formatters,
                session.Id, format: "xml", maxPackets: null,
                allowPartial: false, CancellationToken.None));
    }

    private static CaptureSessionManager NewManager()
    {
        var factory = new Mock<ICaptureSourceFactory>();
        factory.SetupGet(f => f.AvailableSources)
            .Returns(["pcap", "http"]);
        factory.Setup(f => f.Create(It.IsAny<string>(), It.IsAny<string>()))
            .Returns((string source, string folder) =>
                new FakeCaptureSource(packets: [],
                    linkType: source == "http"
                        ? LinkLayers.Null
                        : LinkLayers.Ethernet));
        return new CaptureSessionManager(factory.Object,
            NullLogger<CaptureSessionManager>.Instance);
    }

    private static async Task<(CaptureSessionManager Manager, CaptureSession Session)>
        PrepareSessionWithFakeAsync(string sourceName, LinkLayers link,
            IReadOnlySet<FormatKind> supported, string? rawPcapFilePath = null)
    {
        var factory = new Mock<ICaptureSourceFactory>();
        factory.SetupGet(f => f.AvailableSources).Returns([sourceName]);
        factory.Setup(f => f.Create(It.IsAny<string>(), It.IsAny<string>()))
            .Returns((string _, string _) => new FakeCaptureSource(
                packets:
                [
                    new CapturedPacket(DateTimeOffset.UnixEpoch.AddSeconds(1),
                        4, new byte[] { 1, 2, 3, 4 }, link, kEmpty),
                    new CapturedPacket(DateTimeOffset.UnixEpoch.AddSeconds(2),
                        5, new byte[] { 5, 6, 7, 8, 9 }, link, kEmpty)
                ],
                linkType: link,
                supported: supported,
                rawPcapFilePath: rawPcapFilePath));
        var manager = new CaptureSessionManager(factory.Object,
            NullLogger<CaptureSessionManager>.Instance);
        var session = manager.CreateSession(
            new StartCaptureRequest { Source = sourceName });
        await session.StartAsync(CancellationToken.None);
        await session.StopAsync(CancellationToken.None);
        return (manager, session);
    }

    private static TraceFormatterRegistry NewFormatterRegistry()
    {
        return new TraceFormatterRegistry(new ITraceFormatter[]
        {
            new PcapFormatter(),
            new PcapNgFormatter(),
            new JsonFormatter(),
            new CsvFormatter(),
            new TextFormatter()
        });
    }

    private static byte[] MakeMinimalPcapFile()
    {
        // libpcap global header (24 bytes) for an empty file:
        // magic=0xa1b2c3d4 / major=2 / minor=4 / thiszone=0 / sigfigs=0
        // / snaplen=65535 / linktype=1 (Ethernet)
        var bytes = new byte[24];
        System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(0, 4), 0xa1b2c3d4U);
        System.Buffers.Binary.BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(4, 2), 2);
        System.Buffers.Binary.BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(6, 2), 4);
        System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(16, 4), 65535);
        System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(20, 4), 1);
        return bytes;
    }

    private static readonly IReadOnlyDictionary<string, string?> kEmpty =
        new Dictionary<string, string?>();
}
