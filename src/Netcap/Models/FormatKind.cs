// Licensed under the Apache License, Version 2.0.

namespace Netcap.Models;

using System.Runtime.Serialization;

/// <summary>
/// Supported output formats.
/// </summary>
[DataContract]
public enum FormatKind
{
    [EnumMember(Value = "pcap")]
    Pcap,

    [EnumMember(Value = "pcapng")]
    PcapNg,

    [EnumMember(Value = "json")]
    Json,

    [EnumMember(Value = "csv")]
    Csv,

    [EnumMember(Value = "text")]
    Text
}

public static class FormatKindExtensions
{
    public static string ToWireName(this FormatKind kind) => kind switch
    {
        FormatKind.Pcap => "pcap",
        FormatKind.PcapNg => "pcapng",
        FormatKind.Json => "json",
        FormatKind.Csv => "csv",
        FormatKind.Text => "text",
        _ => kind.ToString()
    };

    public static bool TryParse(string? value, out FormatKind kind)
    {
        var v = value?.Trim() ?? string.Empty;
        if (v.Equals("pcap", StringComparison.OrdinalIgnoreCase))
        { kind = FormatKind.Pcap; return true; }
        if (v.Equals("pcapng", StringComparison.OrdinalIgnoreCase))
        { kind = FormatKind.PcapNg; return true; }
        if (v.Equals("json", StringComparison.OrdinalIgnoreCase))
        { kind = FormatKind.Json; return true; }
        if (v.Equals("csv", StringComparison.OrdinalIgnoreCase))
        { kind = FormatKind.Csv; return true; }
        if (v.Equals("text", StringComparison.OrdinalIgnoreCase))
        { kind = FormatKind.Text; return true; }
        kind = default;
        return false;
    }
}
