// Licensed under the Apache License, Version 2.0.

namespace NetcapMcp;

using Microsoft.Extensions.DependencyInjection;
using NetcapMcp.Capture;
using NetcapMcp.Formats;

/// <summary>
/// Shared DI registrations for both transport modes (stdio and http).
/// </summary>
internal static class NetcapMcpServiceCollectionExtensions
{
    public static IServiceCollection AddNetcapMcp(this IServiceCollection services)
    {
        services.AddSingleton<ICaptureSourceFactory, CaptureSourceFactory>();
        services.AddSingleton<CaptureSessionManager>();
        services.AddHostedService(sp => sp.GetRequiredService<CaptureSessionManager>());

        services.AddSingleton<ITraceFormatter, PcapFormatter>();
        services.AddSingleton<ITraceFormatter, PcapNgFormatter>();
        services.AddSingleton<ITraceFormatter, JsonFormatter>();
        services.AddSingleton<ITraceFormatter, CsvFormatter>();
        services.AddSingleton<ITraceFormatter, TextFormatter>();
        services.AddSingleton<TraceFormatterRegistry>();
        return services;
    }
}
