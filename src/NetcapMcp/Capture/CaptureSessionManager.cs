// Licensed under the Apache License, Version 2.0.

namespace NetcapMcp.Capture;

using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NetcapMcp.Models;

/// <summary>
/// Singleton registry for capture sessions. Owns lifecycle, lookup, limits
/// and shutdown cleanup.
/// </summary>
internal sealed class CaptureSessionManager : IHostedService, IAsyncDisposable
{
    private const int kMaxActiveSessions = 8;
    private const int kMaxRetainedSessions = 32;

    private readonly ConcurrentDictionary<string, CaptureSession> _sessions = new();
    private readonly ICaptureSourceFactory _factory;
    private readonly ILogger<CaptureSessionManager> _logger;
    private readonly string _rootFolder;

    public CaptureSessionManager(ICaptureSourceFactory factory,
        ILogger<CaptureSessionManager> logger)
    {
        _factory = factory;
        _logger = logger;
        _rootFolder = Path.Combine(Path.GetTempPath(),
            "netcap-mcp-" + Path.GetRandomFileName());
        Directory.CreateDirectory(_rootFolder);
    }

    /// <summary>
    /// Create a session, allocate its folder and source instance. Does NOT
    /// start the capture - caller must call <see cref="CaptureSession.StartAsync"/>.
    /// </summary>
    public CaptureSession CreateSession(StartCaptureRequest request)
    {
        var activeCount = _sessions.Values.Count(s =>
            s.State is CaptureSessionState.Starting or CaptureSessionState.Running);
        if (activeCount >= kMaxActiveSessions)
        {
            throw new NetcapMcpException(
                $"Too many active sessions ({activeCount} >= {kMaxActiveSessions}). " +
                "Stop one before starting another.");
        }

        var id = Guid.NewGuid().ToString("N");
        var folder = Path.Combine(_rootFolder, id);
        Directory.CreateDirectory(folder);

        ICaptureSource source;
        try
        {
            source = _factory.Create(request.Source, folder);
        }
        catch
        {
            try { Directory.Delete(folder, true); } catch { /* best effort */ }
            throw;
        }

        var session = new CaptureSession(id, request.Source, source, folder,
            request, _logger);
        _sessions[id] = session;

        EvictIfNecessary();
        return session;
    }

    public bool TryGet(string sessionId,
        [NotNullWhen(true)] out CaptureSession? session)
    {
        return _sessions.TryGetValue(sessionId, out session);
    }

    public CaptureSession Get(string sessionId)
    {
        if (!_sessions.TryGetValue(sessionId, out var s))
        {
            throw new NetcapMcpException($"Session '{sessionId}' not found.");
        }
        return s;
    }

    public IReadOnlyList<CaptureSession> List(CaptureSessionState? include = null)
    {
        var all = _sessions.Values.ToList();
        if (include is null)
        {
            return all;
        }
        return [.. all.Where(s => s.State == include)];
    }

    public async Task RemoveAsync(string sessionId, CancellationToken ct)
    {
        if (_sessions.TryRemove(sessionId, out var session))
        {
            await session.DisposeAsync().ConfigureAwait(false);
        }
    }

    private void EvictIfNecessary()
    {
        if (_sessions.Count <= kMaxRetainedSessions)
        {
            return;
        }
        // Evict oldest non-active sessions (LRU by LastTouchedAt).
        var candidates = _sessions.Values
            .Where(s => s.State is CaptureSessionState.Completed
                or CaptureSessionState.Failed)
            .OrderBy(s => s.LastTouchedAt)
            .ToList();
        var toEvict = _sessions.Count - kMaxRetainedSessions;
        foreach (var c in candidates.Take(toEvict))
        {
#pragma warning disable CA2000 // ownership transferred to DisposeInBackground
            if (_sessions.TryRemove(c.Id, out var removed))
            {
                DisposeInBackground(removed);
            }
#pragma warning restore CA2000
        }
    }

    private void DisposeInBackground(CaptureSession session)
    {
        _ = Task.Run(async () =>
        {
            try
            {
                await session.DisposeAsync().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "Evicted session {SessionId} dispose error.", session.Id);
            }
        });
    }

    public Task StartAsync(CancellationToken cancellationToken)
        => Task.CompletedTask;

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        await DisposeAsync().ConfigureAwait(false);
    }

    public async ValueTask DisposeAsync()
    {
        foreach (var session in _sessions.Values.ToList())
        {
            try
            {
                await session.DisposeAsync().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "Session {SessionId} dispose error.", session.Id);
            }
        }
        _sessions.Clear();
        try
        {
            if (Directory.Exists(_rootFolder))
            {
                Directory.Delete(_rootFolder, recursive: true);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "Root folder cleanup error: {Folder}", _rootFolder);
        }
    }
}
