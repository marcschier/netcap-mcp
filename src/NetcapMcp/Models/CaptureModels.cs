// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace NetcapMcp.Models;

using System.ComponentModel.DataAnnotations;
using System.Runtime.Serialization;

/// <summary>
/// Lifecycle state of a capture session.
/// </summary>
[DataContract]
public enum CaptureSessionState
{
    [EnumMember(Value = "starting")]
    Starting,

    [EnumMember(Value = "running")]
    Running,

    [EnumMember(Value = "stopping")]
    Stopping,

    [EnumMember(Value = "completed")]
    Completed,

    [EnumMember(Value = "failed")]
    Failed,

    [EnumMember(Value = "disposed")]
    Disposed
}

/// <summary>
/// Public-facing description of a capture session.
/// </summary>
[DataContract]
public sealed record class CaptureSessionInfo
{
    [DataMember(Name = "sessionId", Order = 1)]
    [Required]
    public required string SessionId { get; init; }

    [DataMember(Name = "source", Order = 2)]
    [Required]
    public required string Source { get; init; }

    [DataMember(Name = "state", Order = 3)]
    [Required]
    public required CaptureSessionState State { get; init; }

    [DataMember(Name = "startedAt", Order = 4)]
    public DateTimeOffset? StartedAt { get; init; }

    [DataMember(Name = "stoppedAt", Order = 5)]
    public DateTimeOffset? StoppedAt { get; init; }

    [DataMember(Name = "packetCount", Order = 6)]
    public long PacketCount { get; init; }

    [DataMember(Name = "byteCount", Order = 7)]
    public long ByteCount { get; init; }

    [DataMember(Name = "interfaceName", Order = 8)]
    public string? InterfaceName { get; init; }

    [DataMember(Name = "filter", Order = 9)]
    public string? Filter { get; init; }

    [DataMember(Name = "listenUrl", Order = 10)]
    public string? ListenUrl { get; init; }

    [DataMember(Name = "error", Order = 11)]
    public string? Error { get; init; }

    [DataMember(Name = "supportedFormats", Order = 12)]
    public IReadOnlyList<string> SupportedFormats { get; init; } = [];
}

/// <summary>
/// Information about a discoverable network interface.
/// </summary>
[DataContract]
public sealed record class NetworkInterfaceInfo
{
    [DataMember(Name = "name", Order = 1)]
    [Required]
    public required string Name { get; init; }

    [DataMember(Name = "friendlyName", Order = 2)]
    public string? FriendlyName { get; init; }

    [DataMember(Name = "description", Order = 3)]
    public string? Description { get; init; }

    [DataMember(Name = "addresses", Order = 4)]
    public IReadOnlyList<string> Addresses { get; init; } = [];

    [DataMember(Name = "linkType", Order = 5)]
    public string? LinkType { get; init; }

    [DataMember(Name = "isLoopback", Order = 6)]
    public bool IsLoopback { get; init; }
}

/// <summary>
/// Top-N summary of a completed capture.
/// </summary>
[DataContract]
public sealed record class CaptureSummary
{
    [DataMember(Name = "sessionId", Order = 1)]
    [Required]
    public required string SessionId { get; init; }

    [DataMember(Name = "packetCount", Order = 2)]
    public long PacketCount { get; init; }

    [DataMember(Name = "byteCount", Order = 3)]
    public long ByteCount { get; init; }

    [DataMember(Name = "durationSeconds", Order = 4)]
    public double DurationSeconds { get; init; }

    [DataMember(Name = "topSources", Order = 5)]
    public IReadOnlyList<TopEntry> TopSources { get; init; } = [];

    [DataMember(Name = "topDestinations", Order = 6)]
    public IReadOnlyList<TopEntry> TopDestinations { get; init; } = [];

    [DataMember(Name = "topProtocols", Order = 7)]
    public IReadOnlyList<TopEntry> TopProtocols { get; init; } = [];

    [DataMember(Name = "topPorts", Order = 8)]
    public IReadOnlyList<TopEntry> TopPorts { get; init; } = [];
}

[DataContract]
public sealed record class TopEntry
{
    [DataMember(Name = "key", Order = 1)]
    [Required]
    public required string Key { get; init; }

    [DataMember(Name = "count", Order = 2)]
    public long Count { get; init; }

    [DataMember(Name = "bytes", Order = 3)]
    public long Bytes { get; init; }
}
