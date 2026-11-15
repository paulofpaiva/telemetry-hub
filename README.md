# telemetry-hub

Real-time industrial IoT telemetry ingestion API built with .NET 10.

Sensors (pressure, temperature, vibration) publish readings over MQTT. The API validates them,
stores them in a time-series database, exposes query endpoints and periodically archives
aggregated data to S3-compatible object storage. Everything runs locally with Docker, no cloud
account required.

> Work in progress. This README grows with each step and gets the full architecture diagram,
> run instructions and technical decisions at the end.

## Stack

- .NET 10, C#, ASP.NET Core Minimal API
- CQRS with MediatR and FluentValidation
- MQTT (Mosquitto + MQTTnet)
- QuestDB for time-series data
- PostgreSQL + EF Core for the device registry
- S3-compatible storage (MinIO locally) for archives
- Docker Compose, Testcontainers

## Solution layout

```
src/
  TelemetryHub.Domain          Entities and domain rules, no dependencies
  TelemetryHub.Application     Commands, queries, handlers, validators, pipeline behaviors
  TelemetryHub.Infrastructure  MQTT, QuestDB, S3 and EF Core implementations
  TelemetryHub.Api             Minimal API endpoints and composition root
  TelemetryHub.Simulator       Worker that publishes fake sensor readings over MQTT
tests/
  TelemetryHub.UnitTests
  TelemetryHub.IntegrationTests
```

## Build

```bash
dotnet build
dotnet test
```

## License

[MIT](LICENSE)
