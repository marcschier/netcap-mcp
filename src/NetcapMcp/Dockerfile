FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
RUN apt-get update -y \
    && apt-get install -y --no-install-recommends apt-utils \
    && echo 'debconf debconf/frontend select Noninteractive' | debconf-set-selections \
    && apt-get install -y --no-install-recommends libpcap-dev bash net-tools \
    && rm -rf /var/lib/apt/lists/*
USER root:root
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["Directory.Build.props", "Directory.Packages.props", "version.json", "./"]
COPY ["src/", "src/"]
RUN dotnet restore "src/NetcapMcp/NetcapMcp.csproj" \
    -s https://api.nuget.org/v3/index.json

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "src/NetcapMcp/NetcapMcp.csproj" \
    -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
EXPOSE 3001
ENTRYPOINT ["dotnet", "NetcapMcp.dll", "--http", "--port", "3001"]
