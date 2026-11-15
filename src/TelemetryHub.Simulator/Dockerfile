FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /repo

COPY global.json Directory.Build.props Directory.Packages.props ./
COPY src/TelemetryHub.Simulator/TelemetryHub.Simulator.csproj src/TelemetryHub.Simulator/
RUN dotnet restore src/TelemetryHub.Simulator/TelemetryHub.Simulator.csproj

COPY src/TelemetryHub.Simulator/ src/TelemetryHub.Simulator/
RUN dotnet publish src/TelemetryHub.Simulator/TelemetryHub.Simulator.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/runtime:10.0 AS runtime
WORKDIR /app
COPY --from=build /app .
USER $APP_UID
ENTRYPOINT ["dotnet", "TelemetryHub.Simulator.dll"]
