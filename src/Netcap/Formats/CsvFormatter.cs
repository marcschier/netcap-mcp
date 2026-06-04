// Licensed under the Apache License, Version 2.0.

namespace Netcap.Formats;

using System.Globalization;
using System.Text;
using Netcap.Capture;
using Netcap.Models;

internal sealed class CsvFormatter : ITraceFormatter
{
    public FormatKind Kind => FormatKind.Csv;
    public string MimeType => "text/csv";

    public async Task<FormatResult> FormatAsync(CaptureSession session,
        long? maxPackets, CancellationToken ct)
    {
        var sb = new StringBuilder();
        sb.AppendLine(
            "timestamp,length,source,destination,protocol,sourcePort,destinationPort,payloadLength");
        var count = 0L;
        await foreach (var packet in session.Source
            .ReadAllAsync(maxPackets, ct).ConfigureAwait(false))
        {
            ct.ThrowIfCancellationRequested();
            var p = PacketDecoder.Decode(packet);
            sb.Append(p.Timestamp.ToString("O", CultureInfo.InvariantCulture)).Append(',');
            sb.Append(p.Length.ToString(CultureInfo.InvariantCulture)).Append(',');
            sb.Append(Escape(p.Source)).Append(',');
            sb.Append(Escape(p.Destination)).Append(',');
            sb.Append(Escape(p.Protocol)).Append(',');
            sb.Append(p.SourcePort?.ToString(CultureInfo.InvariantCulture)).Append(',');
            sb.Append(p.DestinationPort?.ToString(CultureInfo.InvariantCulture)).Append(',');
            sb.AppendLine(p.PayloadLength?.ToString(CultureInfo.InvariantCulture));
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

    private static string Escape(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }
        if (value.Contains(',', StringComparison.Ordinal) ||
            value.Contains('"', StringComparison.Ordinal) ||
            value.Contains('\n', StringComparison.Ordinal))
        {
            return "\"" + value.Replace("\"", "\"\"", StringComparison.Ordinal) + "\"";
        }
        return value;
    }
}
