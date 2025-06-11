FROM node:latest AS node-build
WORKDIR /src/Frontend

COPY Frontend/package*.json ./
RUN npm install

COPY Frontend/ .
RUN npm run build

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS dotnet-build
WORKDIR /src

COPY EchoPhase/EchoPhase.csproj .
RUN dotnet restore

COPY EchoPhase/ .
COPY --from=node-build /src/Frontend/dist/ ./wwwroot
RUN dotnet publish -c Release -o /app

FROM mcr.microsoft.com/dotnet/aspnet:9.0
WORKDIR /app
COPY --from=dotnet-build /app/ .

ENTRYPOINT ["dotnet", "EchoPhase.dll"]
