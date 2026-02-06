FROM mcr.microsoft.com/dotnet/sdk:10.0 AS dotnet-build

WORKDIR /src
COPY EchoPhase.csproj .
RUN dotnet restore
COPY . .
RUN dotnet publish -c Release -o /app

FROM mcr.microsoft.com/dotnet/aspnet:10.0

WORKDIR /app
COPY --from=dotnet-build /app .
ENTRYPOINT ["dotnet", "EchoPhase.dll"]
