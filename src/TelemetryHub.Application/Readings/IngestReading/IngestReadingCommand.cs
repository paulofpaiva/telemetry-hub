using MediatR;
using TelemetryHub.Domain.Readings;

namespace TelemetryHub.Application.Readings.IngestReading;

/// <summary>Accepts one sensor reading that arrived from the broker.</summary>
public sealed record IngestReadingCommand(
    string DeviceId,
    SensorType SensorType,
    double Value,
    string Unit,
    DateTimeOffset Timestamp) : IRequest;
