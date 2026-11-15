namespace TelemetryHub.Domain.Readings;

/// <summary>A single measurement reported by one sensor of a device.</summary>
public sealed record Reading(
    string DeviceId,
    SensorType SensorType,
    double Value,
    string Unit,
    DateTimeOffset Timestamp);
