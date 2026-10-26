FROM mcr.microsoft.com/dotnet/aspnet:10.0.14@sha256:de0ee358cdd672f7017a003bed21e9ac9ed48a27c922a59b18175b6e82fa7fee AS base
RUN apt-get update -y \
    && apt-get install -y --no-install-recommends apt-utils \
    && echo 'debconf debconf/frontend select Noninteractive' | debconf-set-selections \
    && apt-get install -y --no-install-recommends libpcap-dev bash net-tools \
    && rm -rf /var/lib/apt/lists/*
USER root:root
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:10.0.403@sha256:de4fe90a98845d75910b9769662465c84de1a934665c431279d1382b2bddc02b AS build
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
