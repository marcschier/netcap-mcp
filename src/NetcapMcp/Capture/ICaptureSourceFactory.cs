// Licensed under the Apache License, Version 2.0.

namespace NetcapMcp.Capture;

/// <summary>
/// Factory that resolves a capture source by name (e.g. "pcap", "http").
/// </summary>
internal interface ICaptureSourceFactory
{
    /// <summary>Names of registered sources.</summary>
    IReadOnlyList<string> AvailableSources { get; }

    /// <summary>
    /// Create a new source instance for the given session. Throws
    /// <see cref="NetcapMcpException"/> when the source name is unknown.
    /// </summary>
    ICaptureSource Create(string source, string sessionFolder);
}
