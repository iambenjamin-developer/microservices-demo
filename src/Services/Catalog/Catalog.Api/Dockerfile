# syntax=docker/dockerfile:1

# Multi-stage build: the SDK image compiles, the (much smaller) ASP.NET runtime image ships.
# The build context is the repository root, because a service also needs the shared projects
# and the repository-wide build configuration (global.json, Directory.Build/Packages.props).
#   docker build -f src/Services/Catalog/Catalog.Api/Dockerfile .

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src

# Restore from the project files alone. Sources change on every commit, dependencies almost never,
# so this layer stays cached and the restore is skipped on most builds.
COPY global.json Directory.Build.props Directory.Packages.props ./
COPY src/ServiceDefaults/ServiceDefaults.csproj src/ServiceDefaults/
COPY src/BuildingBlocks/BuildingBlocks.Common/BuildingBlocks.Common.csproj src/BuildingBlocks/BuildingBlocks.Common/
COPY src/BuildingBlocks/BuildingBlocks.Web/BuildingBlocks.Web.csproj src/BuildingBlocks/BuildingBlocks.Web/
COPY src/Services/Catalog/Catalog.Api/Catalog.Api.csproj src/Services/Catalog/Catalog.Api/
RUN dotnet restore src/Services/Catalog/Catalog.Api/Catalog.Api.csproj

COPY src/ServiceDefaults/ src/ServiceDefaults/
COPY src/BuildingBlocks/BuildingBlocks.Common/ src/BuildingBlocks/BuildingBlocks.Common/
COPY src/BuildingBlocks/BuildingBlocks.Web/ src/BuildingBlocks/BuildingBlocks.Web/
COPY src/Services/Catalog/ src/Services/Catalog/
RUN dotnet publish src/Services/Catalog/Catalog.Api/Catalog.Api.csproj \
    --configuration $BUILD_CONFIGURATION --no-restore --output /app

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=build /app .

# APP_UID comes from the base image: the process runs as a non-root user, and 8080 is the
# unprivileged port the .NET images listen on by default.
USER $APP_UID
EXPOSE 8080
ENTRYPOINT ["dotnet", "Catalog.Api.dll"]
