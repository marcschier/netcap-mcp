// Licensed under the Apache License, Version 2.0.

namespace Netcap.Tests;

using System.Net.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Netcap.Capture;
using Netcap.Models;
using Xunit;

public sealed class HttpCaptureSourceTests
{
    [Fact]
    public async Task RecordsRequestsAndRedactsSensitiveHeaders()
    {
        var folder = Path.Combine(Path.GetTempPath(),
            "netcapmcp-test-" + Path.GetRandomFileName());
        Directory.CreateDirectory(folder);
        await using var source = new HttpCaptureSource(folder,
            NullLogger<HttpCaptureSource>.Instance);
        await source.StartAsync(
            new StartCaptureRequest { Source = "http" },
            CancellationToken.None);

        var listenUrl = source.ListenUrl!;
        Assert.NotNull(listenUrl);
        Assert.StartsWith("http://127.0.0.1:", listenUrl,
            StringComparison.Ordinal);

        using (var client = new HttpClient())
        {
            using var req1 = new HttpRequestMessage(HttpMethod.Get,
                listenUrl + "ping?foo=bar");
            req1.Headers.Add("Authorization", "Bearer s3cret-token");
            req1.Headers.Add("X-Trace-Id", "abc-123");
            using var resp1 = await client.SendAsync(req1);
            Assert.Equal(System.Net.HttpStatusCode.NoContent, resp1.StatusCode);

            using var req2 = new HttpRequestMessage(HttpMethod.Post,
                listenUrl + "hello");
            req2.Content = new StringContent("payload-body");
            req2.Headers.Add("Cookie", "session=abc");
            using var resp2 = await client.SendAsync(req2);
            Assert.Equal(System.Net.HttpStatusCode.NoContent, resp2.StatusCode);
        }

        // Allow background JSONL writes to flush.
        for (var i = 0; i < 20 && source.PacketCount < 2; i++)
        {
            await Task.Delay(50);
        }

        await source.StopAsync(CancellationToken.None);

        Assert.Equal(2, source.PacketCount);

        var packets = new List<CapturedPacket>();
        await foreach (var p in source.ReadAllAsync(null, CancellationToken.None))
        {
            packets.Add(p);
        }
        Assert.Equal(2, packets.Count);

        var first = packets[0];
        Assert.Equal("GET", first.Annotations["method"]);
        Assert.Contains("ping?foo=bar", first.Annotations["rawUrl"] ?? "",
            StringComparison.Ordinal);

        var headers1 = first.Annotations["headers"] ?? "";
        Assert.Contains("X-Trace-Id", headers1, StringComparison.Ordinal);
        Assert.DoesNotContain("s3cret-token", headers1,
            StringComparison.Ordinal);
        Assert.Contains("redacted", headers1, StringComparison.OrdinalIgnoreCase);

        var second = packets[1];
        Assert.Equal("POST", second.Annotations["method"]);
        var headers2 = second.Annotations["headers"] ?? "";
        Assert.DoesNotContain("session=abc", headers2,
            StringComparison.Ordinal);

        try { Directory.Delete(folder, true); } catch { /* best effort */ }
    }

    [Fact]
    public async Task RejectsOutOfRangePort()
    {
        var folder = Path.Combine(Path.GetTempPath(),
            "netcapmcp-test-" + Path.GetRandomFileName());
        Directory.CreateDirectory(folder);
        await using var source = new HttpCaptureSource(folder,
            NullLogger<HttpCaptureSource>.Instance);

        await Assert.ThrowsAsync<NetcapException>(() =>
            source.StartAsync(
                new StartCaptureRequest
                {
                    Source = "http",
                    ListenPort = 70000
                },
                CancellationToken.None));

        try { Directory.Delete(folder, true); } catch { /* best effort */ }
    }
}
