using System.ComponentModel.DataAnnotations;

namespace TelemetryHub.Infrastructure.Mqtt;

public sealed class MqttOptions
{
    public const string SectionName = "Mqtt";

    [Required]
    public string Host { get; set; } = string.Empty;

    [Range(1, 65535)]
    public int Port { get; set; } = 1883;

    public string? Username { get; set; }

    public string? Password { get; set; }

    /// <summary>First topic level. Readings arrive on "{TopicPrefix}/{deviceId}/{sensorType}".</summary>
    [Required]
    public string TopicPrefix { get; set; } = "telemetry";

    [Required]
    public string ClientId { get; set; } = "telemetry-hub-api";

    public string TopicFilter => $"{TopicPrefix}/+/+";
}
