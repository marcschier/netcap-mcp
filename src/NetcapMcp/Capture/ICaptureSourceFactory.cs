// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

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
