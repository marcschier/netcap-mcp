// Licensed under the Apache License, Version 2.0.

namespace Netcap;

using System;
using Microsoft.Extensions.Logging;
using PacketDotNet;

/// <summary>
/// Source-generated structured log messages used by the Netcap library.
/// Centralising them here lets the LoggerMessage source generator produce
/// allocation-free, strongly-typed logging methods and satisfies CA1848.
/// </summary>
internal static partial class Log
{
    [LoggerMessage(EventId = 1000, Level = LogLevel.Information,
        Message = "Session {SessionId} started ({Source}).")]
    public static partial void SessionStarted(ILogger logger,
        string sessionId, string source);

    [LoggerMessage(EventId = 1001, Level = LogLevel.Error,
        Message = "Session {SessionId} failed to start.")]
    public static partial void SessionStartFailed(ILogger logger,
        Exception exception, string sessionId);

    [LoggerMessage(EventId = 1002, Level = LogLevel.Information,
        Message = "Session {SessionId} completed ({Packets} packets, {Bytes} bytes).")]
    public static partial void SessionCompleted(ILogger logger,
        string sessionId, long packets, long bytes);

    [LoggerMessage(EventId = 1003, Level = LogLevel.Error,
        Message = "Session {SessionId} failed to stop.")]
    public static partial void SessionStopFailed(ILogger logger,
        Exception exception, string sessionId);

    [LoggerMessage(EventId = 1004, Level = LogLevel.Warning,
        Message = "Session {SessionId} dispose error.")]
    public static partial void SessionDisposeError(ILogger logger,
        Exception exception, string sessionId);

    [LoggerMessage(EventId = 1005, Level = LogLevel.Warning,
        Message = "Session {SessionId} folder cleanup error.")]
    public static partial void SessionFolderCleanupError(ILogger logger,
        Exception exception, string sessionId);

    [LoggerMessage(EventId = 1006, Level = LogLevel.Warning,
        Message = "Evicted session {SessionId} dispose error.")]
    public static partial void EvictedSessionDisposeError(ILogger logger,
        Exception exception, string sessionId);

    [LoggerMessage(EventId = 1007, Level = LogLevel.Warning,
        Message = "Root folder cleanup error: {Folder}")]
    public static partial void RootFolderCleanupError(ILogger logger,
        Exception exception, string folder);

    [LoggerMessage(EventId = 2000, Level = LogLevel.Information,
        Message = "pcap source capturing on {Device} ({LinkType}) filter={Filter}")]
    public static partial void PcapSourceCapturing(ILogger logger,
        string device, LinkLayers linkType, string filter);

    [LoggerMessage(EventId = 2001, Level = LogLevel.Information,
        Message = "Failed promiscuous open on {Device}, falling back to normal mode.")]
    public static partial void PcapPromiscuousFallback(ILogger logger,
        string device);

    [LoggerMessage(EventId = 3000, Level = LogLevel.Information,
        Message = "http source listening on {Url}")]
    public static partial void HttpSourceListening(ILogger logger,
        string url);

    [LoggerMessage(EventId = 3001, Level = LogLevel.Debug,
        Message = "http source request handling error")]
    public static partial void HttpRequestHandlingError(ILogger logger,
        Exception exception);
}
