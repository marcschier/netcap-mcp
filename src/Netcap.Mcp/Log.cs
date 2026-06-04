// Licensed under the Apache License, Version 2.0.

namespace Netcap.Mcp;

using Microsoft.Extensions.Logging;

/// <summary>
/// Source-generated structured log messages used by the Netcap.Mcp host.
/// </summary>
internal static partial class Log
{
    [LoggerMessage(EventId = 4000, Level = LogLevel.Warning,
        Message = "Netcap.Mcp HTTP transport listening on {Bind}:{Port} with "
            + "loopback restriction DISABLED. Local-dev only.")]
    public static partial void HttpTransportLoopbackDisabled(ILogger logger,
        string bind, int port);

    [LoggerMessage(EventId = 4001, Level = LogLevel.Information,
        Message = "Netcap.Mcp HTTP transport ready at {Url}")]
    public static partial void HttpTransportReady(ILogger logger, string url);
}
