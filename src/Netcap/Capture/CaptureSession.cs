// Licensed under the Apache License, Version 2.0.

namespace Netcap.Capture;

using Microsoft.Extensions.Logging;
using Netcap.Models;

/// <summary>
/// Wraps an <see cref="ICaptureSource"/> with state machine, per-session
/// lock and metadata. Lifecycle:
/// Starting -> Running -> Stopping -> (Completed | Failed) -> Disposed.
/// </summary>
internal sealed class CaptureSession : IAsyncDisposable
{
    public string Id { get; }
    public string SourceName { get; }
    public ICaptureSource Source { get; }
    public string SessionFolder { get; }
    public StartCaptureRequest Request { get; }

    public CaptureSessionState State { get; private set; } = CaptureSessionState.Starting;
    public DateTimeOffset? StartedAt { get; private set; }
    public DateTimeOffset? StoppedAt { get; private set; }
    public DateTimeOffset LastTouchedAt { get; private set; } = DateTimeOffset.UtcNow;
    public string? Error { get; private set; }

    private readonly SemaphoreSlim _lock = new(1, 1);
    private readonly ILogger _logger;

    public CaptureSession(string id, string sourceName, ICaptureSource source,
        string sessionFolder, StartCaptureRequest request, ILogger logger)
    {
        Id = id;
        SourceName = sourceName;
        Source = source;
        SessionFolder = sessionFolder;
        Request = request;
        _logger = logger;
    }

    /// <summary>
    /// Acquire the per-session lock. Caller must dispose the returned token.
    /// </summary>
    public async Task<IDisposable> AcquireAsync(CancellationToken ct)
    {
        await _lock.WaitAsync(ct).ConfigureAwait(false);
        return new Releaser(_lock, this);
    }

    public async Task StartAsync(CancellationToken ct)
    {
        using var _ = await AcquireAsync(ct).ConfigureAwait(false);
        try
        {
            await Source.StartAsync(Request, ct).ConfigureAwait(false);
            StartedAt = DateTimeOffset.UtcNow;
            State = CaptureSessionState.Running;
            Log.SessionStarted(_logger, Id, SourceName);
        }
        catch (Exception ex)
        {
            Error = ex.Message;
            State = CaptureSessionState.Failed;
            Log.SessionStartFailed(_logger, ex, Id);
            throw;
        }
    }

    public async Task StopAsync(CancellationToken ct)
    {
        using var _ = await AcquireAsync(ct).ConfigureAwait(false);
        if (State is CaptureSessionState.Completed or CaptureSessionState.Failed
            or CaptureSessionState.Disposed)
        {
            return;
        }
        State = CaptureSessionState.Stopping;
        try
        {
            await Source.StopAsync(ct).ConfigureAwait(false);
            StoppedAt = DateTimeOffset.UtcNow;
            State = CaptureSessionState.Completed;
            Log.SessionCompleted(_logger, Id, Source.PacketCount, Source.ByteCount);
        }
        catch (Exception ex)
        {
            Error = ex.Message;
            State = CaptureSessionState.Failed;
            Log.SessionStopFailed(_logger, ex, Id);
            throw;
        }
    }

    public CaptureSessionInfo ToInfo()
    {
        return new CaptureSessionInfo
        {
            SessionId = Id,
            Source = SourceName,
            State = State,
            StartedAt = StartedAt,
            StoppedAt = StoppedAt,
            PacketCount = Source.PacketCount,
            ByteCount = Source.ByteCount,
            InterfaceName = Request.InterfaceName,
            Filter = Request.BpfFilter,
            ListenUrl = Source.ListenUrl,
            Error = Error,
            SupportedFormats = [.. Source.SupportedFormats.Select(f => f.ToWireName())]
        };
    }

    public async ValueTask DisposeAsync()
    {
        try
        {
            await Source.DisposeAsync().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Log.SessionDisposeError(_logger, ex, Id);
        }
        try
        {
            if (Directory.Exists(SessionFolder))
            {
                Directory.Delete(SessionFolder, recursive: true);
            }
        }
        catch (Exception ex)
        {
            Log.SessionFolderCleanupError(_logger, ex, Id);
        }
        State = CaptureSessionState.Disposed;
        _lock.Dispose();
    }

    private sealed class Releaser : IDisposable
    {
        private readonly SemaphoreSlim _sem;
        private readonly CaptureSession _session;
        private int _disposed;

        public Releaser(SemaphoreSlim sem, CaptureSession session)
        {
            _sem = sem;
            _session = session;
        }

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0)
            {
                return;
            }
            _session.LastTouchedAt = DateTimeOffset.UtcNow;
            _sem.Release();
        }
    }
}
