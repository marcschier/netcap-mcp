// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace NetcapMcp;

using System;
using ModelContextProtocol;

/// <summary>
/// Exception type used by the sample to signal user-facing errors that
/// should be surfaced through MCP tool responses. Derives from
/// <see cref="McpException"/> so its message is forwarded by the MCP
/// server runtime as a JSON-RPC error.
/// </summary>
internal sealed class NetcapMcpException : McpException
{
    public NetcapMcpException(string message)
        : base(message)
    {
    }

    public NetcapMcpException(string message, Exception inner)
        : base(message, inner)
    {
    }
}


