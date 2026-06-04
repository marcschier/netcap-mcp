// Licensed under the Apache License, Version 2.0.

namespace NetcapMcp.Capture;

using Microsoft.Extensions.Logging;
using NetcapMcp.Models;

/// <summary>
/// Default <see cref="ICaptureSourceFactory"/> with built-in pcap and http
/// sources. To add a new source, register it via <see cref="Register"/>.
/// </summary>
internal sealed class CaptureSourceFactory : ICaptureSourceFactory
{
    private readonly Dictionary<string, Func<string, ICaptureSource>> _factories =
        new(StringComparer.OrdinalIgnoreCase);

    public CaptureSourceFactory(ILoggerFactory loggerFactory)
    {
        Register(CaptureSourceNames.Pcap, folder =>
            new PcapCaptureSource(folder,
                loggerFactory.CreateLogger<PcapCaptureSource>()));
        Register(CaptureSourceNames.Http, folder =>
            new HttpCaptureSource(folder,
                loggerFactory.CreateLogger<HttpCaptureSource>()));
    }

    public IReadOnlyList<string> AvailableSources => [.. _factories.Keys];

    public ICaptureSource Create(string source, string sessionFolder)
    {
        if (!_factories.TryGetValue(source, out var f))
        {
            throw new NetcapMcpException(
                $"Unknown capture source '{source}'. Available: {string.Join(", ", AvailableSources)}.");
        }
        return f(sessionFolder);
    }

    public void Register(string name, Func<string, ICaptureSource> factory)
    {
        _factories[name] = factory;
    }
}
