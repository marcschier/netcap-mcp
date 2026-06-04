// Licensed under the Apache License, Version 2.0.

namespace Netcap.Tests;

using System.Collections.Generic;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Netcap.Capture;
using Netcap.Models;
using Xunit;

public sealed class CaptureSessionManagerTests
{
    [Fact]
    public void CreateSession_AssignsUniqueIdAndFolder()
    {
        using var harness = new ManagerHarness();
        var s1 = harness.Manager.CreateSession(NewRequest());
        var s2 = harness.Manager.CreateSession(NewRequest());

        Assert.NotEqual(s1.Id, s2.Id);
        Assert.True(Directory.Exists(s1.SessionFolder));
        Assert.True(Directory.Exists(s2.SessionFolder));
        Assert.Equal(CaptureSessionState.Starting, s1.State);
    }

    [Fact]
    public async Task StartStop_TransitionsThroughExpectedStates()
    {
        using var harness = new ManagerHarness();
        var session = harness.Manager.CreateSession(NewRequest());

        await session.StartAsync(CancellationToken.None);
        Assert.Equal(CaptureSessionState.Running, session.State);
        Assert.NotNull(session.StartedAt);

        await session.StopAsync(CancellationToken.None);
        Assert.Equal(CaptureSessionState.Completed, session.State);
        Assert.NotNull(session.StoppedAt);
    }

    [Fact]
    public async Task StopAsync_IsIdempotent()
    {
        using var harness = new ManagerHarness();
        var session = harness.Manager.CreateSession(NewRequest());
        await session.StartAsync(CancellationToken.None);
        await session.StopAsync(CancellationToken.None);

        await session.StopAsync(CancellationToken.None);

        Assert.Equal(CaptureSessionState.Completed, session.State);
    }

    [Fact]
    public void CreateSession_ThrowsWhenActiveLimitExceeded()
    {
        using var harness = new ManagerHarness();
        // Default kMaxActiveSessions is 8.
        var sessions = new List<CaptureSession>();
        for (var i = 0; i < 8; i++)
        {
            sessions.Add(harness.Manager.CreateSession(NewRequest()));
        }

        Assert.Throws<NetcapException>(
            () => harness.Manager.CreateSession(NewRequest()));
    }

    [Fact]
    public async Task DisposeAsync_RemovesSessionFolder()
    {
        using var harness = new ManagerHarness();
        var session = harness.Manager.CreateSession(NewRequest());
        var folder = session.SessionFolder;

        await harness.Manager.RemoveAsync(session.Id, CancellationToken.None);

        Assert.False(Directory.Exists(folder));
        Assert.False(harness.Manager.TryGet(session.Id, out _));
    }

    [Fact]
    public async Task Get_ThrowsWhenSessionMissing()
    {
        using var harness = new ManagerHarness();
        Assert.Throws<NetcapException>(() => harness.Manager.Get("missing"));
        await Task.CompletedTask;
    }

    [Fact]
    public async Task List_FiltersByState()
    {
        using var harness = new ManagerHarness();
        var running = harness.Manager.CreateSession(NewRequest());
        await running.StartAsync(CancellationToken.None);
        var completed = harness.Manager.CreateSession(NewRequest());
        await completed.StartAsync(CancellationToken.None);
        await completed.StopAsync(CancellationToken.None);

        var all = harness.Manager.List();
        var run = harness.Manager.List(CaptureSessionState.Running);
        var done = harness.Manager.List(CaptureSessionState.Completed);

        Assert.Equal(2, all.Count);
        Assert.Single(run);
        Assert.Single(done);
        Assert.Equal(running.Id, run[0].Id);
        Assert.Equal(completed.Id, done[0].Id);
    }

    [Fact]
    public async Task ConcurrentStartStop_DoesNotCorruptState()
    {
        using var harness = new ManagerHarness();
        var session = harness.Manager.CreateSession(NewRequest());
        await session.StartAsync(CancellationToken.None);

        // Race a bunch of stop calls; they should all complete cleanly
        // and the final state should be Completed.
        var tasks = Enumerable.Range(0, 8)
            .Select(_ => session.StopAsync(CancellationToken.None))
            .ToArray();
        await Task.WhenAll(tasks);

        Assert.Equal(CaptureSessionState.Completed, session.State);
    }

    private static StartCaptureRequest NewRequest()
        => new() { Source = "fake" };

    private sealed class ManagerHarness : IDisposable
    {
        public CaptureSessionManager Manager { get; }

        public ManagerHarness()
        {
            var factory = new Mock<ICaptureSourceFactory>(MockBehavior.Strict);
            factory.SetupGet(f => f.AvailableSources).Returns(["fake"]);
            factory.Setup(f => f.Create(It.IsAny<string>(), It.IsAny<string>()))
                .Returns((string _, string _) => new FakeCaptureSource(packets: []));
            Manager = new CaptureSessionManager(factory.Object,
                NullLogger<CaptureSessionManager>.Instance);
        }

        public void Dispose()
        {
            Manager.DisposeAsync().AsTask().GetAwaiter().GetResult();
        }
    }
}
