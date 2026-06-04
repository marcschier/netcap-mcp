// Licensed under the Apache License, Version 2.0.

namespace Netcap.Formats;

using System.Globalization;
using System.Text;
using System.Text.Json;
using Netcap.Capture;
using Netcap.Models;

internal sealed class JsonFormatter : ITraceFormatter
{
    public FormatKind Kind => FormatKind.Json;
    public string MimeType => "application/json";

    public async Task<FormatResult> FormatAsync(CaptureSession session,
        long? maxPackets, CancellationToken ct)
    {
        using var ms = new MemoryStream();
        var writer = new Utf8JsonWriter(ms,
            new JsonWriterOptions { Indented = false });
        await using var disposer = writer.ConfigureAwait(false);
        writer.WriteStartArray();

        var count = 0L;
        await foreach (var packet in session.Source
            .ReadAllAsync(maxPackets, ct).ConfigureAwait(false))
        {
            ct.ThrowIfCancellationRequested();
            var decoded = PacketDecoder.Decode(packet);
            WriteRecord(writer, decoded);
            count++;
        }

        writer.WriteEndArray();
        await writer.FlushAsync(ct).ConfigureAwait(false);
        return new FormatResult
        {
            Kind = Kind,
            MimeType = MimeType,
            Text = Encoding.UTF8.GetString(ms.ToArray()),
            PacketsFormatted = count
        };
    }

    private static void WriteRecord(Utf8JsonWriter writer, DecodedPacket p)
    {
        writer.WriteStartObject();
        writer.WriteString("timestamp",
            p.Timestamp.ToString("O", CultureInfo.InvariantCulture));
        writer.WriteNumber("length", p.Length);
        if (p.Source is not null) writer.WriteString("source", p.Source);
        if (p.Destination is not null) writer.WriteString("destination", p.Destination);
        if (p.Protocol is not null) writer.WriteString("protocol", p.Protocol);
        if (p.SourcePort is int sp) writer.WriteNumber("sourcePort", sp);
        if (p.DestinationPort is int dp) writer.WriteNumber("destinationPort", dp);
        if (p.PayloadLength is int pl) writer.WriteNumber("payloadLength", pl);
        if (p.Annotations is not null && p.Annotations.Count > 0)
        {
            writer.WriteStartObject("annotations");
            foreach (var kv in p.Annotations)
            {
                if (kv.Value is null)
                {
                    writer.WriteNull(kv.Key);
                }
                else
                {
                    writer.WriteString(kv.Key, kv.Value);
                }
            }
            writer.WriteEndObject();
        }
        writer.WriteEndObject();
    }
}
