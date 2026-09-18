# syntax=docker/dockerfile:1

# See src/Services/Catalog/Catalog.Api/Dockerfile for the shape of these files; the build context
# is the repository root:
#   docker build -f src/Services/Ordering/Ordering.Api/Dockerfile .

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src

COPY global.json Directory.Build.props Directory.Packages.props ./
COPY src/ServiceDefaults/ServiceDefaults.csproj src/ServiceDefaults/
COPY src/BuildingBlocks/BuildingBlocks.Common/BuildingBlocks.Common.csproj src/BuildingBlocks/BuildingBlocks.Common/
COPY src/BuildingBlocks/BuildingBlocks.Contracts/BuildingBlocks.Contracts.csproj src/BuildingBlocks/BuildingBlocks.Contracts/
COPY src/BuildingBlocks/BuildingBlocks.Messaging/BuildingBlocks.Messaging.csproj src/BuildingBlocks/BuildingBlocks.Messaging/
COPY src/BuildingBlocks/BuildingBlocks.Web/BuildingBlocks.Web.csproj src/BuildingBlocks/BuildingBlocks.Web/
COPY src/Services/Ordering/Ordering.Domain/Ordering.Domain.csproj src/Services/Ordering/Ordering.Domain/
COPY src/Services/Ordering/Ordering.Application/Ordering.Application.csproj src/Services/Ordering/Ordering.Application/
COPY src/Services/Ordering/Ordering.Infrastructure/Ordering.Infrastructure.csproj src/Services/Ordering/Ordering.Infrastructure/
COPY src/Services/Ordering/Ordering.Api/Ordering.Api.csproj src/Services/Ordering/Ordering.Api/
RUN dotnet restore src/Services/Ordering/Ordering.Api/Ordering.Api.csproj

COPY src/ServiceDefaults/ src/ServiceDefaults/
COPY src/BuildingBlocks/ src/BuildingBlocks/
COPY src/Services/Ordering/ src/Services/Ordering/
RUN dotnet publish src/Services/Ordering/Ordering.Api/Ordering.Api.csproj \
    --configuration $BUILD_CONFIGURATION --no-restore --output /app

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=build /app .
USER $APP_UID
EXPOSE 8080
ENTRYPOINT ["dotnet", "Ordering.Api.dll"]
