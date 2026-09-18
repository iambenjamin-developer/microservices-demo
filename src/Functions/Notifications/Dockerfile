# syntax=docker/dockerfile:1

# Azure Functions isolated worker. The build stage is the same as the other services; the runtime
# image is the Functions host, which owns the Service Bus receive loop and the HTTP server, and
# runs the published worker it finds in /home/site/wwwroot.
# The build context is the repository root:
#   docker build -f src/Functions/Notifications/Dockerfile .

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src

COPY global.json Directory.Build.props Directory.Packages.props ./
COPY src/ServiceDefaults/ServiceDefaults.csproj src/ServiceDefaults/
COPY src/BuildingBlocks/BuildingBlocks.Common/BuildingBlocks.Common.csproj src/BuildingBlocks/BuildingBlocks.Common/
COPY src/BuildingBlocks/BuildingBlocks.Contracts/BuildingBlocks.Contracts.csproj src/BuildingBlocks/BuildingBlocks.Contracts/
COPY src/BuildingBlocks/BuildingBlocks.Messaging/BuildingBlocks.Messaging.csproj src/BuildingBlocks/BuildingBlocks.Messaging/
COPY src/BuildingBlocks/BuildingBlocks.Web/BuildingBlocks.Web.csproj src/BuildingBlocks/BuildingBlocks.Web/
COPY src/Functions/Notifications/Notifications.csproj src/Functions/Notifications/
RUN dotnet restore src/Functions/Notifications/Notifications.csproj

COPY src/ServiceDefaults/ src/ServiceDefaults/
COPY src/BuildingBlocks/ src/BuildingBlocks/
COPY src/Functions/Notifications/ src/Functions/Notifications/
RUN dotnet publish src/Functions/Notifications/Notifications.csproj \
    --configuration $BUILD_CONFIGURATION --no-restore --output /app

FROM mcr.microsoft.com/azure-functions/dotnet-isolated:4-dotnet-isolated10.0 AS final

# The base image listens on port 80; 8080 keeps every service in the compose file on one port.
ENV AzureWebJobsScriptRoot=/home/site/wwwroot \
    AzureFunctionsJobHost__Logging__Console__IsEnabled=true \
    ASPNETCORE_URLS=http://+:8080

COPY --from=build /app /home/site/wwwroot
EXPOSE 8080
