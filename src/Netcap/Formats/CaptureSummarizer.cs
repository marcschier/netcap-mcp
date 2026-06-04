// Licensed under the Apache License, Version 2.0.

namespace Netcap.Formats;

using Netcap.Capture;
using Netcap.Models;

/// <summary>
/// Computes top-N talkers, protocols and ports for a completed session.
/// </summary>
internal static class CaptureSummarizer
{
    public static async Task<CaptureSummary> SummarizeAsync(CaptureSession session,
        int topN, CancellationToken ct)
    {
        if (topN <= 0)
        {
            topN = 10;
        }
        var sources = new Dictionary<string, (long Count, long Bytes)>(StringComparer.Ordinal);
        var dests = new Dictionary<string, (long Count, long Bytes)>(StringComparer.Ordinal);
        var protos = new Dictionary<string, (long Count, long Bytes)>(StringComparer.Ordinal);
        var ports = new Dictionary<string, (long Count, long Bytes)>(StringComparer.Ordinal);

        await foreach (var packet in session.Source
            .ReadAllAsync(maxPackets: null, ct).ConfigureAwait(false))
        {
            ct.ThrowIfCancellationRequested();
            var p = PacketDecoder.Decode(packet);
            Bump(sources, p.Source, p.Length);
            Bump(dests, p.Destination, p.Length);
            Bump(protos, p.Protocol, p.Length);
            if (p.SourcePort is int sp)
            {
                Bump(ports, sp.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    p.Length);
            }
            if (p.DestinationPort is int dp)
            {
                Bump(ports, dp.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    p.Length);
            }
        }

        var duration = (session.StoppedAt ?? DateTimeOffset.UtcNow) -
            (session.StartedAt ?? DateTimeOffset.UtcNow);

        return new CaptureSummary
        {
            SessionId = session.Id,
            PacketCount = session.Source.PacketCount,
            ByteCount = session.Source.ByteCount,
            DurationSeconds = duration.TotalSeconds,
            TopSources = Top(sources, topN),
            TopDestinations = Top(dests, topN),
            TopProtocols = Top(protos, topN),
            TopPorts = Top(ports, topN)
        };
    }

    private static void Bump(Dictionary<string, (long Count, long Bytes)> dict,
        string? key, int bytes)
    {
        if (string.IsNullOrEmpty(key))
        {
            return;
        }
        dict.TryGetValue(key, out var cur);
        dict[key] = (cur.Count + 1, cur.Bytes + bytes);
    }

    private static List<TopEntry> Top(
        Dictionary<string, (long Count, long Bytes)> dict, int n)
    {
        return [.. dict
            .OrderByDescending(kv => kv.Value.Count)
            .ThenByDescending(kv => kv.Value.Bytes)
            .Take(n)
            .Select(kv => new TopEntry
            {
                Key = kv.Key,
                Count = kv.Value.Count,
                Bytes = kv.Value.Bytes
            })];
    }
}
