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

RUN dotnet publish EchoPhase/EchoPhase.csproj -c Release -o /app

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=dotnet-build /app .
ENTRYPOINT ["dotnet", "EchoPhase.dll"]
