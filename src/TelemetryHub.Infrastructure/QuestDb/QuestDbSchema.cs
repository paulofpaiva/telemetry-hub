namespace TelemetryHub.Infrastructure.QuestDb;

internal static class QuestDbSchema
{
    public const string ReadingsTable = "readings";

    /// <summary>
    /// Created explicitly instead of relying on ILP auto-creation so partitioning and
    /// deduplication are under our control. DEDUP makes writes idempotent, which matters because
    /// MQTT QoS 1 can deliver the same reading more than once.
    /// </summary>
    public const string CreateReadingsTable = $"""
        CREATE TABLE IF NOT EXISTS {ReadingsTable} (
            timestamp TIMESTAMP,
            device_id SYMBOL,
            sensor_type SYMBOL,
            unit SYMBOL,
            value DOUBLE
        ) TIMESTAMP(timestamp) PARTITION BY DAY WAL
        DEDUP UPSERT KEYS(timestamp, device_id, sensor_type);
        """;
}
