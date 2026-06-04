// Licensed under the Apache License, Version 2.0.

namespace NetcapMcp.Models;

using System.ComponentModel.DataAnnotations;
using System.Runtime.Serialization;

/// <summary>
/// Source kind identifiers used by the capture tools.
/// </summary>
public static class CaptureSourceNames
{
    public const string Pcap = "pcap";
    public const string Http = "http";
}

/// <summary>
/// Common shape for starting a capture. Source-specific fields are validated
/// per <see cref="Source"/>.
/// </summary>
[DataContract]
public sealed record class StartCaptureRequest
{
    [DataMember(Name = "source", Order = 1)]
    [Required]
    public required string Source { get; init; }

    [DataMember(Name = "interfaceName", Order = 2)]
    public string? InterfaceName { get; init; }

    [DataMember(Name = "bpfFilter", Order = 3)]
    public string? BpfFilter { get; init; }

    [DataMember(Name = "promiscuous", Order = 4)]
    public bool? Promiscuous { get; init; }

    [DataMember(Name = "listenPort", Order = 5)]
    public int? ListenPort { get; init; }

    [DataMember(Name = "pathPrefix", Order = 6)]
    public string? PathPrefix { get; init; }

    [DataMember(Name = "maxBytes", Order = 7)]
    public long? MaxBytes { get; init; }

    [DataMember(Name = "maxPackets", Order = 8)]
    public long? MaxPackets { get; init; }

    [DataMember(Name = "maxDurationSeconds", Order = 9)]
    public int? MaxDurationSeconds { get; init; }
}

/// <summary>
/// Request for capture_now (start + sleep + stop + format in one call).
/// </summary>
[DataContract]
public sealed record class CaptureNowRequest
{
    [DataMember(Name = "source", Order = 1)]
    [Required]
    public required string Source { get; init; }

    [DataMember(Name = "format", Order = 2)]
    [Required]
    public required string Format { get; init; }

    [DataMember(Name = "durationSeconds", Order = 3)]
    [Required]
    public required int DurationSeconds { get; init; }

    [DataMember(Name = "interfaceName", Order = 4)]
    public string? InterfaceName { get; init; }

    [DataMember(Name = "bpfFilter", Order = 5)]
    public string? BpfFilter { get; init; }

    [DataMember(Name = "promiscuous", Order = 6)]
    public bool? Promiscuous { get; init; }

    [DataMember(Name = "listenPort", Order = 7)]
    public int? ListenPort { get; init; }

    [DataMember(Name = "pathPrefix", Order = 8)]
    public string? PathPrefix { get; init; }

    [DataMember(Name = "maxBytes", Order = 9)]
    public long? MaxBytes { get; init; }

    [DataMember(Name = "maxPackets", Order = 10)]
    public long? MaxPackets { get; init; }

    internal StartCaptureRequest ToStartRequest()
    {
        return new StartCaptureRequest
        {
            Source = Source,
            InterfaceName = InterfaceName,
            BpfFilter = BpfFilter,
            Promiscuous = Promiscuous,
            ListenPort = ListenPort,
            PathPrefix = PathPrefix,
            MaxBytes = MaxBytes,
            MaxPackets = MaxPackets,
            MaxDurationSeconds = DurationSeconds
        };
    }
}
