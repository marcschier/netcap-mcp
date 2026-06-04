// Licensed under the Apache License, Version 2.0.

namespace NetcapMcp.Formats;

using System.Globalization;
using System.Text;
using NetcapMcp.Capture;
using NetcapMcp.Models;

/// <summary>
/// One line per packet, Wireshark-summary style:
/// <c>{ts} {src} -&gt; {dst} {proto} len={n}</c>.
/// </summary>
internal sealed class TextFormatter : ITraceFormatter
{
    public FormatKind Kind => FormatKind.Text;
    public string MimeType => "text/plain";

    public async Task<FormatResult> FormatAsync(CaptureSession session,
        long? maxPackets, CancellationToken ct)
    {
        var sb = new StringBuilder();
        var count = 0L;
        await foreach (var packet in session.Source
            .ReadAllAsync(maxPackets, ct).ConfigureAwait(false))
        {
            ct.ThrowIfCancellationRequested();
            var p = PacketDecoder.Decode(packet);
            sb.Append(p.Timestamp.ToString("HH:mm:ss.ffffff",
                CultureInfo.InvariantCulture));
            sb.Append(' ');
            sb.Append(p.Source ?? "?");
            if (p.SourcePort is int sp)
            {
                sb.Append(':').Append(sp.ToString(CultureInfo.InvariantCulture));
            }
            sb.Append(" -> ");
            sb.Append(p.Destination ?? "?");
            if (p.DestinationPort is int dp)
            {
                sb.Append(':').Append(dp.ToString(CultureInfo.InvariantCulture));
            }
            sb.Append(' ');
            sb.Append(p.Protocol ?? "?");
            sb.Append(" len=").Append(p.Length.ToString(CultureInfo.InvariantCulture));
            if (p.Annotations is not null &&
                p.Annotations.TryGetValue("method", out var method))
            {
                sb.Append(' ').Append(method);
                if (p.Annotations.TryGetValue("rawUrl", out var url))
                {
                    sb.Append(' ').Append(url);
                }
            }
            sb.AppendLine();
            count++;
        }
        return new FormatResult
        {
            Kind = Kind,
            MimeType = MimeType,
            Text = sb.ToString(),
            PacketsFormatted = count
        };
    }
}
