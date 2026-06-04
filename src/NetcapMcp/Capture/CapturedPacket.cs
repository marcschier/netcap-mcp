// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace NetcapMcp.Capture;

using PacketDotNet;

/// <summary>
/// A single captured record produced by a capture source.
/// </summary>
/// <param name="Timestamp">When the record was captured.</param>
/// <param name="OriginalLength">Original on-wire length of the packet (may
/// be greater than <c>Data.Length</c> if the capture was snaplen-truncated).</param>
/// <param name="Data">Raw link-layer bytes when available (pcap source). May
/// be empty for non-frame sources (e.g. HTTP).</param>
/// <param name="LinkType">Link layer type of <see cref="Data"/>, or
/// <c>LinkLayers.Null</c> for non-frame sources.</param>
/// <param name="Annotations">Optional key/value metadata supplied by the
/// source (e.g. HTTP method/url).</param>
internal sealed record class CapturedPacket(
    DateTimeOffset Timestamp,
    int OriginalLength,
    ReadOnlyMemory<byte> Data,
    LinkLayers LinkType,
    IReadOnlyDictionary<string, string?> Annotations);
