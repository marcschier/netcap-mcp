// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace NetcapMcp;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NetcapMcp.Mcp;

/// <summary>
/// Entry point. Selects between the stdio MCP transport (default) and the
/// HTTP MCP transport via command-line switch.
/// </summary>
internal static class Program
{
    private const int kDefaultHttpPort = 3001;

    public static async Task<int> Main(string[] args)
    {
        try
        {
            var options = CommandLineOptions.Parse(args);
            if (options.ShowHelp)
            {
                await PrintUsageAsync().ConfigureAwait(false);
                return 0;
            }

            return options.Transport switch
            {
                TransportMode.Stdio => await RunStdioAsync(args).ConfigureAwait(false),
                TransportMode.Http => await RunHttpAsync(args, options).ConfigureAwait(false),
                _ => throw new NetcapMcpException("Unsupported transport.")
            };
        }
        catch (NetcapMcpException ex)
        {
            await Console.Error.WriteLineAsync(ex.Message).ConfigureAwait(false);
            return 2;
        }
        catch (Exception ex)
        {
            await Console.Error.WriteLineAsync("Fatal: " + ex)
                .ConfigureAwait(false);
            return 1;
        }
    }

    private static async Task<int> RunStdioAsync(string[] args)
    {
        // Stdio mode: stdout is reserved for the MCP framed stream. All
        // logging must go to stderr; no banners, no Console.Out writes.
        var builder = Host.CreateApplicationBuilder(args);
        builder.Logging.ClearProviders();
        builder.Logging.AddConsole(o =>
            o.LogToStandardErrorThreshold = LogLevel.Trace);
        builder.Services.AddNetcapMcp();
        builder.Services
            .AddMcpServer()
            .WithStdioServerTransport()
            .WithTools<CaptureTools>();
        await builder.Build().RunAsync().ConfigureAwait(false);
        return 0;
    }

    private static async Task<int> RunHttpAsync(string[] args, CommandLineOptions options)
    {
        var port = options.Port ?? kDefaultHttpPort;
        var bind = options.UnsafeListenAny ? "0.0.0.0" : "127.0.0.1";
        var url = $"http://{bind}:{port}";

        var builder = WebApplication.CreateBuilder(args);
        builder.WebHost.UseUrls(url);
        builder.Services.AddNetcapMcp();
        builder.Services
            .AddMcpServer()
            .WithHttpTransport(o => o.Stateless = true)
            .WithTools<CaptureTools>();

        var app = builder.Build();
        var logger = app.Services
            .GetRequiredService<ILoggerFactory>()
            .CreateLogger("NetcapMcp");
        if (!options.UnsafeListenAny)
        {
            app.Use(async (ctx, next) =>
            {
                var remote = ctx.Connection.RemoteIpAddress;
                if (remote is null ||
                    !System.Net.IPAddress.IsLoopback(remote))
                {
                    ctx.Response.StatusCode = StatusCodes.Status403Forbidden;
                    await ctx.Response.WriteAsync(
                        "NetcapMcp HTTP transport accepts loopback only. " +
                        "Re-run with --unsafe-listen-any to override.")
                        .ConfigureAwait(false);
                    return;
                }
                await next(ctx).ConfigureAwait(false);
            });
        }
        else
        {
            logger.LogWarning(
                "NetcapMcp HTTP transport listening on {Bind}:{Port} with " +
                "loopback restriction DISABLED. Local-dev only.", bind, port);
        }

        app.MapMcp();
        if (logger.IsEnabled(LogLevel.Information))
        {
            logger.LogInformation("NetcapMcp HTTP transport ready at {Url}", url);
        }
        await app.RunAsync().ConfigureAwait(false);
        return 0;
    }

    private static Task PrintUsageAsync()
    {
        var help = """
            NetcapMcp - Model Context Protocol server for network trace capture.

            Usage:
              NetcapMcp [--stdio]                       Run with stdio transport (default)
              NetcapMcp --http [--port <n>] [--unsafe-listen-any]
                                                        Run with HTTP transport
              NetcapMcp --help                          Show this help

            Transport options:
              --stdio                Stdio MCP transport (default if no switch).
              --http                 HTTP MCP transport (ASP.NET Core).
              --port <n>             HTTP port (default 3001).
              --unsafe-listen-any    Bind HTTP to 0.0.0.0 instead of 127.0.0.1.
                                     Local development only - the server is
                                     unauthenticated.

            Notes:
              * In stdio mode no banner is written to stdout - that stream is
                reserved for the MCP framed protocol. All logging goes to stderr.
              * pcap capture requires NET_ADMIN/root on Linux or
                Administrator on Windows.
            """;
        return Console.Error.WriteLineAsync(help);
    }

    private enum TransportMode { Stdio, Http }

    private sealed record class CommandLineOptions(TransportMode Transport,
        int? Port, bool UnsafeListenAny, bool ShowHelp)
    {
        public static CommandLineOptions Parse(string[] args)
        {
            var transport = TransportMode.Stdio;
            int? port = null;
            var unsafeAny = false;
            var help = false;
            for (var i = 0; i < args.Length; i++)
            {
                switch (args[i])
                {
                    case "--stdio":
                        transport = TransportMode.Stdio;
                        break;
                    case "--http":
                        transport = TransportMode.Http;
                        break;
                    case "--port":
                        if (i + 1 >= args.Length ||
                            !int.TryParse(args[i + 1],
                                System.Globalization.NumberStyles.Integer,
                                System.Globalization.CultureInfo.InvariantCulture,
                                out var p))
                        {
                            throw new NetcapMcpException(
                                "--port requires a numeric value.");
                        }
                        port = p;
                        i++;
                        break;
                    case "--unsafe-listen-any":
                        unsafeAny = true;
                        break;
                    case "--help":
                    case "-h":
                    case "/?":
                        help = true;
                        break;
                    default:
                        // Unknown args are passed through to the host builder.
                        break;
                }
            }
            return new CommandLineOptions(transport, port, unsafeAny, help);
        }
    }
}

