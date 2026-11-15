FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /repo

COPY global.json Directory.Build.props Directory.Packages.props ./
COPY src/TelemetryHub.Domain/TelemetryHub.Domain.csproj src/TelemetryHub.Domain/
COPY src/TelemetryHub.Application/TelemetryHub.Application.csproj src/TelemetryHub.Application/
COPY src/TelemetryHub.Infrastructure/TelemetryHub.Infrastructure.csproj src/TelemetryHub.Infrastructure/
COPY src/TelemetryHub.Api/TelemetryHub.Api.csproj src/TelemetryHub.Api/
RUN dotnet restore src/TelemetryHub.Api/TelemetryHub.Api.csproj

COPY src/ src/
RUN dotnet publish src/TelemetryHub.Api/TelemetryHub.Api.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app .
USER $APP_UID
EXPOSE 8080
ENTRYPOINT ["dotnet", "TelemetryHub.Api.dll"]
