# Netcap

[![ci](https://github.com/marcschier/netcap/actions/workflows/ci.yml/badge.svg)](https://github.com/marcschier/netcap/actions/workflows/ci.yml)
[![License: Apache 2.0](https://img.shields.io/badge/license-Apache--2.0-blue.svg)](LICENSE)
[![Netcap.Mcp on GitHub Packages](https://img.shields.io/badge/GitHub%20Packages-Netcap.Mcp-2088FF?logo=github)](https://github.com/marcschier/netcap/pkgs/nuget/Netcap.Mcp)
[![Netcap on GitHub Packages](https://img.shields.io/badge/GitHub%20Packages-Netcap-2088FF?logo=github)](https://github.com/marcschier/netcap/pkgs/nuget/Netcap)

A **Model Context Protocol** server that captures network traces and
returns them as **pcap**, **pcapng**, **JSON**, **CSV**, or **text** — driven
by an MCP-aware client (LLM agent, IDE, CLI tool). Installs as a
[.NET tool](https://learn.microsoft.com/dotnet/core/tools/global-tools)
or runs in Docker.

> ⚠️ Packet capture is invasive and the resulting traces may contain
> sensitive data. Treat them with care.

## Packages

Both packages are public, published to **GitHub Packages** (NuGet feed)
on every push to `main` by the [CI workflow](.github/workflows/ci.yml).
The feed is `https://nuget.pkg.github.com/marcschier/index.json`.

| Package        | Feed                                                                                                       | Purpose                                                                                                                          |
|----------------|------------------------------------------------------------------------------------------------------------|----------------------------------------------------------------------------------------------------------------------------------|
| **`Netcap`**     | [github.com/marcschier/netcap/pkgs/nuget/Netcap](https://github.com/marcschier/netcap/pkgs/nuget/Netcap)         | Capture engine library — `ICaptureSource`, pcap & passive-http sources, pcap/pcapng/json/csv/text formatters, session manager.   |
| **`Netcap.Mcp`** | [github.com/marcschier/netcap/pkgs/nuget/Netcap.Mcp](https://github.com/marcschier/netcap/pkgs/nuget/Netcap.Mcp) | MCP server that exposes the engine as MCP tools. Packaged as a `dotnet tool` with command **`netcap-mcp`** (stdio + HTTP).        |

Add the feed to your NuGet sources once:

```bash
dotnet nuget add source \
    --name marcschier \
    "https://nuget.pkg.github.com/marcschier/index.json"
```

After this, every example below works against the GitHub Packages feed.

## Features

- **Multi-source capture** via a pluggable `ICaptureSource` abstraction:
  - `pcap` — single-interface [SharpPcap](https://github.com/dotpcap/sharppcap)
    capture from a named NIC with optional BPF filter.
  - `http` — a passive HTTP listener that records inbound request
    metadata to a JSONL file. **Does not forward requests** anywhere, so
    it cannot be abused as an open proxy.
- **Five output formats**: `pcap` (binary), `pcapng` (binary, written by
  a built-in minimal writer), `json`, `csv`, `text`.
- **Two MCP transports** in a single executable, selected by CLI:
  - `--stdio` (default) — for MCP clients that launch the server as a
    subprocess.
  - `--http` — HTTP transport via ASP.NET Core (Streamable HTTP).
- **Hard safety limits**: ≤ 10 MB response payload, ≤ 50 MB capture
  default, ≤ 30 minutes per `start_capture`, ≤ 60 s `capture_now`,
  ≤ 8 active sessions, LRU eviction beyond 32 retained sessions.

## Register the MCP server in an MCP client via `dnx` (.NET 10)

.NET 10 ships a `dnx` script that runs a .NET tool **without a global
install** — a one-shot launcher in the spirit of `npx`. Wire
`netcap-mcp` into an MCP client config like this:

```jsonc
{
  "mcpServers": {
    "netcap": {
      "command": "dnx",
      "args": [
        "--yes",
        "--source", "https://nuget.pkg.github.com/marcschier/index.json",
        "Netcap.Mcp",
        "--stdio"
      ]
    }
  }
}
```

Notes:
- `dnx` forwards to `dotnet tool exec`. The first invocation downloads
  the `Netcap.Mcp` package; `--yes` skips the per-download confirmation
  prompt.
- The `--source` argument can be omitted once the feed is added to your
  global `NuGet.config` (see [Packages](#packages)).
- Pin a specific version with `Netcap.Mcp@1.0.0-pre-NNN` — see the
  [package page](https://github.com/marcschier/netcap/pkgs/nuget/Netcap.Mcp)
  for available versions.
- Anything after the package name is passed straight to the tool, so
  `--stdio` (or `--http --port 3001`) lands on the server.
- Requires the .NET 10 SDK on the machine running the MCP client.

## Install

### As a global .NET tool (from GitHub Packages)

```bash
dotnet tool install --global Netcap.Mcp --source marcschier
netcap-mcp --stdio                       # default
netcap-mcp --http --port 3001            # ASP.NET Core HTTP transport
netcap-mcp --help
```

`--source marcschier` matches the source name added via `dotnet nuget
add source` above. Without that step you can pass the feed URL inline:
`--source https://nuget.pkg.github.com/marcschier/index.json`.

### From source

```bash
git clone https://github.com/marcschier/netcap.git
cd netcap
dotnet run --project src/Netcap.Mcp -- --stdio
```

### Docker

```bash
docker build -t netcap-mcp -f src/Netcap.Mcp/Dockerfile .
docker run --rm -it --cap-add=NET_ADMIN -p 127.0.0.1:3001:3001 netcap-mcp
```

`--cap-add=NET_ADMIN` is required for the `pcap` source on Linux. The
Docker build context is the repo root because the Dockerfile copies
`Directory.Build.props`, `Directory.Packages.props`, `version.json`, and
both `src/Netcap/` and `src/Netcap.Mcp/`.

## Tools

| Tool | Description |
|------|-------------|
| `list_interfaces`   | Enumerate NICs (use `name` as `interfaceName` for `pcap`). |
| `start_capture`     | Begin a session. `source=pcap`: requires `interfaceName`, optional `bpfFilter`. `source=http`: optional `listenPort`. Returns the session id. |
| `stop_capture`      | Stop and finalise. Safe to read the trace once this returns. |
| `list_captures`     | List sessions. Filter with `state` ∈ `active`, `completed`, `all`. |
| `get_capture`       | Return the trace in `pcap` / `pcapng` / `json` / `csv` / `text`. Binary formats come back as `EmbeddedResourceBlock` + `BlobResourceContents`. |
| `capture_now`       | Start + sleep + stop + format in one call (cleanup guaranteed). |
| `summarize_capture` | Counts plus top-N talkers / protocols / ports for a completed session. |

## Transports

- **stdio** is the default. In stdio mode `stdout` is reserved for the
  framed MCP stream — there is no startup banner and all logging is sent
  to `stderr`.
- **HTTP** binds to `127.0.0.1` by default. A middleware also rejects
  non-loopback requests with `403`. The flag `--unsafe-listen-any` binds
  to all interfaces (local development only — the server is
  unauthenticated).

## Security & limitations

- The **HTTP MCP transport is unauthenticated**; treat HTTP mode as a
  local-dev convenience.
- The **`http` capture source is a passive listener**, not a proxy. It
  returns `204 No Content` to every request, never forwards. No CONNECT,
  no TLS interception.
- `Authorization` / `Cookie` / `Set-Cookie` headers are **redacted** in
  recorded HTTP source events.
- **Pcap captures are sensitive data.** The on-disk libpcap file may
  contain user payloads. Treat session folders accordingly.
- pcap capture requires **`NET_ADMIN` / root on Linux**, **Administrator
  on Windows**, and a libpcap / Npcap install.
- Sessions are in-memory only — restart loses sessions and deletes temp
  files.
- Pcap captures use a **single interface per session** to avoid
  mixed-link-type pcap files. Use multiple sessions for multiple NICs.
- `pcapng` is produced by a built-in minimal writer
  (Section Header + Interface Description + Enhanced Packet blocks).

## Project layout

```
netcap/
├── Netcap.slnx
├── Directory.Build.props
├── Directory.Packages.props
├── version.json
├── README.md
├── LICENSE
├── .github/workflows/ci.yml
├── src/
│   ├── Netcap/                       capture engine library
│   │   ├── Netcap.csproj
│   │   ├── NetcapException.cs
│   │   ├── Capture/                  ICaptureSource, sources, session manager
│   │   ├── Formats/                  pcap/pcapng/json/csv/text formatters + decoder
│   │   └── Models/                   request / response DTOs
│   └── Netcap.Mcp/                   MCP server (dotnet tool)
│       ├── Netcap.Mcp.csproj
│       ├── Program.cs                CLI + transport selection
│       ├── CaptureTools.cs           [McpServerTool] methods
│       ├── ServiceCollectionExtensions.cs
│       ├── Dockerfile
│       └── .dockerignore
└── tests/
    ├── Netcap.Tests/                 capture engine tests (xUnit + Moq)
    └── Netcap.Mcp.Tests/             MCP-tool layer tests
```

## Adding a new capture source

1. Implement `ICaptureSource` in `src/Netcap/Capture/`.
2. Register it in `CaptureSourceFactory` under a new name.
3. Declare which `FormatKind`s your source supports via
   `SupportedFormats` — formatters honour this set.

## Build & test

```bash
dotnet restore -s https://api.nuget.org/v3/index.json
dotnet build  -c Release
dotnet test   -c Release
dotnet pack   src/Netcap.Mcp/Netcap.Mcp.csproj -c Release -o artifacts
```

CI runs the same on `ubuntu-latest` and `windows-latest` on every push
to `main` and every pull request — see
[`.github/workflows/ci.yml`](.github/workflows/ci.yml).

## License

Apache-2.0 — see [`LICENSE`](LICENSE).
