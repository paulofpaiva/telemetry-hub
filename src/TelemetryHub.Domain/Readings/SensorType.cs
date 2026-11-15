namespace TelemetryHub.Domain.Readings;

public enum SensorType
{
    Pressure = 1,
    Temperature = 2,
    Vibration = 3,
}

public static class SensorTypeExtensions
{
    /// <summary>Lowercase name used in MQTT topics, storage keys and the time-series table.</summary>
    public static string ToSlug(this SensorType sensorType) => sensorType switch
    {
        SensorType.Pressure => "pressure",
        SensorType.Temperature => "temperature",
        SensorType.Vibration => "vibration",
        _ => throw new ArgumentOutOfRangeException(nameof(sensorType), sensorType, "Unknown sensor type."),
    };

    public static bool TryParseSlug(string? value, out SensorType sensorType)
    {
        switch (value?.Trim().ToLowerInvariant())
        {
            case "pressure":
                sensorType = SensorType.Pressure;
                return true;
            case "temperature":
                sensorType = SensorType.Temperature;
                return true;
            case "vibration":
                sensorType = SensorType.Vibration;
                return true;
            default:
                sensorType = default;
                return false;
        }
    }
}
