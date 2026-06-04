// Licensed under the Apache License, Version 2.0.

namespace NetcapMcp.Tests;

using NetcapMcp.Models;
using Xunit;

public sealed class FormatKindTests
{
    [Theory]
    [InlineData("pcap", FormatKind.Pcap)]
    [InlineData("PCAP", FormatKind.Pcap)]
    [InlineData("pcapng", FormatKind.PcapNg)]
    [InlineData("PcapNg", FormatKind.PcapNg)]
    [InlineData("json", FormatKind.Json)]
    [InlineData("JSON", FormatKind.Json)]
    [InlineData("csv", FormatKind.Csv)]
    [InlineData("text", FormatKind.Text)]
    [InlineData(" text ", FormatKind.Text)]
    public void TryParse_ReturnsExpectedKind_ForKnownAliases(string input, FormatKind expected)
    {
        Assert.True(FormatKindExtensions.TryParse(input, out var kind));
        Assert.Equal(expected, kind);
    }

    [Theory]
    [InlineData("xml")]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("har")]
    public void TryParse_ReturnsFalse_ForUnknownAliases(string? input)
    {
        Assert.False(FormatKindExtensions.TryParse(input, out _));
    }

    [Theory]
    [InlineData(FormatKind.Pcap, "pcap")]
    [InlineData(FormatKind.PcapNg, "pcapng")]
    [InlineData(FormatKind.Json, "json")]
    [InlineData(FormatKind.Csv, "csv")]
    [InlineData(FormatKind.Text, "text")]
    public void ToWireName_RoundTrips(FormatKind kind, string expected)
    {
        var wire = kind.ToWireName();
        Assert.Equal(expected, wire);
        Assert.True(FormatKindExtensions.TryParse(wire, out var roundTripped));
        Assert.Equal(kind, roundTripped);
    }
}
