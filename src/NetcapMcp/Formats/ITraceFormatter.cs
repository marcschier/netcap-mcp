// Licensed under the Apache License, Version 2.0.

namespace NetcapMcp.Formats;

using NetcapMcp.Capture;
using NetcapMcp.Models;

/// <summary>
/// Format result: either binary bytes or text.
/// </summary>
internal sealed record class FormatResult
{
    public required FormatKind Kind { get; init; }
    public required string MimeType { get; init; }
    public byte[]? Bytes { get; init; }
    public string? Text { get; init; }
    public required long PacketsFormatted { get; init; }

    public bool IsBinary => Bytes is not null;

    public long ByteSize => IsBinary
        ? Bytes!.LongLength
        : System.Text.Encoding.UTF8.GetByteCount(Text ?? string.Empty);
}

internal interface ITraceFormatter
{
    FormatKind Kind { get; }
    string MimeType { get; }
    Task<FormatResult> FormatAsync(CaptureSession session, long? maxPackets,
        CancellationToken ct);
}
