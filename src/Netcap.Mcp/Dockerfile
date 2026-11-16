FROM mcr.microsoft.com/dotnet/aspnet:10.0.15@sha256:bfd912c83295ce66d4b706ebb8541b9f14259b0e76fbb88b2751f58ce2399d2e AS base
RUN apt-get update -y \
    && apt-get install -y --no-install-recommends apt-utils \
    && echo 'debconf debconf/frontend select Noninteractive' | debconf-set-selections \
    && apt-get install -y --no-install-recommends libpcap-dev bash net-tools \
    && rm -rf /var/lib/apt/lists/*
USER root:root
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:10.0.404@sha256:fe0fd4e93bd2fbb47c883b7a6369e5c8d3db8bf01cdc5981f6ee35fbdf3ff4fb AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["Directory.Build.props", "Directory.Packages.props", "version.json", "./"]
COPY ["src/Netcap/", "src/Netcap/"]
COPY ["src/Netcap.Mcp/", "src/Netcap.Mcp/"]
RUN dotnet restore "src/Netcap.Mcp/Netcap.Mcp.csproj" \
    -s https://api.nuget.org/v3/index.json

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "src/Netcap.Mcp/Netcap.Mcp.csproj" \
    -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
EXPOSE 3001
ENTRYPOINT ["dotnet", "Netcap.Mcp.dll", "--http", "--port", "3001"]
