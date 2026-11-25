# No `# syntax=` line: BuildKit's built-in Dockerfile frontend (pinned with
# BuildKit itself) builds it, so a build does not fetch a frontend image from
# Docker Hub each time.

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS dotnet-build
WORKDIR /src

# Restore first, from the project files alone, so the layer is reused until
# a dependency changes. --parents keeps each csproj in its own folder.
COPY EchoPhase.slnx Directory.Build.props Directory.Packages.props NuGet.config ./
COPY packages ./packages
COPY --parents */*.csproj ./

RUN dotnet restore EchoPhase.slnx

COPY . .

# --no-restore: restored above already. No version here: it goes into the
# image below, so a new tag alone does not publish again.
RUN dotnet publish EchoPhase.Server/EchoPhase.Server.csproj -c Release -o /app --no-restore

# The API contract, from the published build above (its XML docs give the
# descriptions): `--opt target=contract --output type=local,dest=artifacts`.
# The release pipeline builds it next to the image, so BuildKit shares the
# restore and the publish with it instead of a second, Debug build.
FROM dotnet-build AS openapi
RUN dotnet tool restore --tool-manifest dotnet-tools.json
WORKDIR /src/EchoPhase.Server
RUN ASPNETCORE_ENVIRONMENT=Development \
    dotnet swagger tofile --output /contract/openapi.json /app/EchoPhase.Server.dll v1

FROM scratch AS contract
COPY --from=openapi /contract/openapi.json /

# The image (the default target: the last stage).
FROM mcr.microsoft.com/dotnet/aspnet:10.0
# Links the published package to its repository (and its visibility) on GHCR.
LABEL org.opencontainers.image.source=https://github.com/EchoPhase-Project/server
WORKDIR /app
COPY --from=dotnet-build /app .
# Runs as the image's unprivileged "app" user; file logs go to /app/Logs.
RUN mkdir -p /app/Logs && chown "$APP_UID:$APP_UID" /app/Logs
# The image tag (scripts/image-tag.sh): the version the server reports
# (ServerVersion). Last, so only this layer changes from one tag to the next.
ARG ECHOPHASE_VERSION=0.0.0-local
ENV ECHOPHASE_VERSION=$ECHOPHASE_VERSION
USER $APP_UID
ENTRYPOINT ["dotnet", "EchoPhase.Server.dll"]
