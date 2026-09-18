# syntax=docker/dockerfile:1

# See src/Services/Catalog/Catalog.Api/Dockerfile for the shape of these files; the build context
# is the repository root:
#   docker build -f src/Gateway/Dockerfile .

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src

COPY global.json Directory.Build.props Directory.Packages.props ./
COPY src/ServiceDefaults/ServiceDefaults.csproj src/ServiceDefaults/
COPY src/BuildingBlocks/BuildingBlocks.Common/BuildingBlocks.Common.csproj src/BuildingBlocks/BuildingBlocks.Common/
COPY src/BuildingBlocks/BuildingBlocks.Web/BuildingBlocks.Web.csproj src/BuildingBlocks/BuildingBlocks.Web/
COPY src/Gateway/Gateway.csproj src/Gateway/
RUN dotnet restore src/Gateway/Gateway.csproj

COPY src/ServiceDefaults/ src/ServiceDefaults/
COPY src/BuildingBlocks/BuildingBlocks.Common/ src/BuildingBlocks/BuildingBlocks.Common/
COPY src/BuildingBlocks/BuildingBlocks.Web/ src/BuildingBlocks/BuildingBlocks.Web/
COPY src/Gateway/ src/Gateway/
RUN dotnet publish src/Gateway/Gateway.csproj \
    --configuration $BUILD_CONFIGURATION --no-restore --output /app

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=build /app .
USER $APP_UID
EXPOSE 8080
ENTRYPOINT ["dotnet", "Gateway.dll"]
