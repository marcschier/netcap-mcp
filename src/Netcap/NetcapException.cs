// Licensed under the Apache License, Version 2.0.

namespace Netcap;

using System;
using ModelContextProtocol;

/// <summary>
/// Exception type used by the sample to signal user-facing errors that
/// should be surfaced through MCP tool responses. Derives from
/// <see cref="McpException"/> so its message is forwarded by the MCP
/// server runtime as a JSON-RPC error.
/// </summary>
internal sealed class NetcapException : McpException
{
    public NetcapException(string message)
        : base(message)
    {
    }

    public NetcapException(string message, Exception inner)
        : base(message, inner)
    {
    }
}


