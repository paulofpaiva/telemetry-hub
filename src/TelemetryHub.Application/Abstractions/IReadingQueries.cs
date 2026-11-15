using TelemetryHub.Domain.Readings;

namespace TelemetryHub.Application.Abstractions;

/// <summary>Read side of the time-series store.</summary>
public interface IReadingQueries
{
    /// <summary>The most recent reading of each sensor of a device.</summary>
    Task<IReadOnlyList<Reading>> GetLatestAsync(string deviceId, CancellationToken cancellationToken);

    /// <summary>Readings of one sensor aggregated into fixed windows, oldest first.</summary>
    Task<IReadOnlyList<ReadingWindow>> GetSeriesAsync(
        string deviceId,
        SensorType sensorType,
        DateTimeOffset from,
        DateTimeOffset to,
        SampleInterval interval,
        CancellationToken cancellationToken);
}

/// <summary>Aggregated values for one time window. <see cref="Timestamp"/> is the window start.</summary>
public sealed record ReadingWindow(DateTimeOffset Timestamp, double Avg, double Min, double Max, long Count);
