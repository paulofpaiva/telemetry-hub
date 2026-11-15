using Dapper;
using TelemetryHub.Application.Abstractions;
using TelemetryHub.Domain.Readings;

namespace TelemetryHub.Infrastructure.QuestDb;

/// <summary>
/// Reads from QuestDB over the PostgreSQL wire protocol with Dapper. The queries rely on
/// QuestDB's time-series SQL extensions (LATEST ON, SAMPLE BY), so they are written by hand.
/// </summary>
internal sealed class QuestDbReadingQueries(QuestDbDataSource dataSource) : IReadingQueries
{
    public async Task<IReadOnlyList<Reading>> GetLatestAsync(string deviceId, CancellationToken cancellationToken)
    {
        const string sql = $"""
            SELECT timestamp AS Timestamp, sensor_type AS SensorType, unit AS Unit, value AS Value
            FROM {QuestDbSchema.ReadingsTable}
            WHERE device_id = @deviceId
            LATEST ON timestamp PARTITION BY sensor_type
            """;

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var rows = await connection.QueryAsync<LatestRow>(
            new CommandDefinition(sql, new { deviceId }, cancellationToken: cancellationToken));

        var readings = new List<Reading>();
        foreach (var row in rows)
        {
            // Skip sensor types this version of the API does not know instead of failing the request.
            if (SensorTypeExtensions.TryParseSlug(row.SensorType, out var sensorType))
            {
                readings.Add(new Reading(deviceId, sensorType, row.Value, row.Unit ?? string.Empty, AsUtc(row.Timestamp)));
            }
        }

        return readings;
    }

    public async Task<IReadOnlyList<ReadingWindow>> GetSeriesAsync(
        string deviceId,
        SensorType sensorType,
        DateTimeOffset from,
        DateTimeOffset to,
        SampleInterval interval,
        CancellationToken cancellationToken)
    {
        // SAMPLE BY does not accept a bind parameter. SampleInterval only ever renders digits
        // followed by one of s/m/h/d, so interpolating it is safe.
        var sql = $"""
            SELECT timestamp AS Timestamp, avg(value) AS Avg, min(value) AS Min, max(value) AS Max, count() AS Count
            FROM {QuestDbSchema.ReadingsTable}
            WHERE device_id = @deviceId
              AND sensor_type = @sensorType
              AND timestamp >= @from
              AND timestamp < @to
            SAMPLE BY {interval} ALIGN TO CALENDAR
            ORDER BY timestamp
            """;

        var parameters = new
        {
            deviceId,
            sensorType = sensorType.ToSlug(),
            from = from.UtcDateTime,
            to = to.UtcDateTime,
        };

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var rows = await connection.QueryAsync<WindowRow>(
            new CommandDefinition(sql, parameters, cancellationToken: cancellationToken));

        return [.. rows.Select(row => new ReadingWindow(AsUtc(row.Timestamp), row.Avg, row.Min, row.Max, row.Count))];
    }

    public async Task<IReadOnlyList<DeviceSensorAggregate>> GetAggregatesAsync(
        DateTimeOffset from,
        DateTimeOffset to,
        CancellationToken cancellationToken)
    {
        const string sql = $"""
            SELECT device_id AS DeviceId, sensor_type AS SensorType, unit AS Unit,
                   avg(value) AS Avg, min(value) AS Min, max(value) AS Max, count() AS Count
            FROM {QuestDbSchema.ReadingsTable}
            WHERE timestamp >= @from AND timestamp < @to
            GROUP BY device_id, sensor_type, unit
            """;

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var rows = await connection.QueryAsync<AggregateRow>(
            new CommandDefinition(sql, new { from = from.UtcDateTime, to = to.UtcDateTime }, cancellationToken: cancellationToken));

        return
        [
            .. rows.Select(row => new DeviceSensorAggregate(
                row.DeviceId,
                row.SensorType,
                row.Unit ?? string.Empty,
                row.Avg,
                row.Min,
                row.Max,
                row.Count)),
        ];
    }

    // QuestDB timestamps are UTC but arrive without a time zone.
    private static DateTimeOffset AsUtc(DateTime timestamp) =>
        new(DateTime.SpecifyKind(timestamp, DateTimeKind.Utc));

    private sealed class LatestRow
    {
        public DateTime Timestamp { get; set; }

        public string SensorType { get; set; } = string.Empty;

        public string? Unit { get; set; }

        public double Value { get; set; }
    }

    private sealed class WindowRow
    {
        public DateTime Timestamp { get; set; }

        public double Avg { get; set; }

        public double Min { get; set; }

        public double Max { get; set; }

        public long Count { get; set; }
    }

    private sealed class AggregateRow
    {
        public string DeviceId { get; set; } = string.Empty;

        public string SensorType { get; set; } = string.Empty;

        public string? Unit { get; set; }

        public double Avg { get; set; }

        public double Min { get; set; }

        public double Max { get; set; }

        public long Count { get; set; }
    }
}
