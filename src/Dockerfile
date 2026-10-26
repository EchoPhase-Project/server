# syntax=docker/dockerfile:1

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS dotnet-build
WORKDIR /src

# Restore first, from the project files alone, so the layer is reused until
# a dependency changes. --parents keeps each csproj in its own folder.
COPY EchoPhase.slnx Directory.Build.props Directory.Packages.props NuGet.config ./
COPY packages ./packages
COPY --parents */*.csproj ./

RUN dotnet restore EchoPhase.slnx

COPY . .

# The image tag (scripts/image-tag.sh): the version the server reports. Not
# named VERSION: a build argument is an environment variable in RUN, and
# MSBuild would take that one as $(Version), which must be a NuGet version.
ARG ECHOPHASE_VERSION=0.0.0-local
# --no-restore: restored above already.
RUN dotnet publish EchoPhase.Server/EchoPhase.Server.csproj -c Release -o /app --no-restore \
    -p:EchoPhaseVersion="$ECHOPHASE_VERSION"

FROM mcr.microsoft.com/dotnet/aspnet:10.0
# Links the published package to its repository (and its visibility) on GHCR.
LABEL org.opencontainers.image.source=https://github.com/EchoPhase-Project/server
WORKDIR /app
COPY --from=dotnet-build /app .
# Runs as the image's unprivileged "app" user; file logs go to /app/Logs.
RUN mkdir -p /app/Logs && chown "$APP_UID:$APP_UID" /app/Logs
USER $APP_UID
ENTRYPOINT ["dotnet", "EchoPhase.Server.dll"]
