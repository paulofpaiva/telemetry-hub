using System.ComponentModel.DataAnnotations;

namespace TelemetryHub.Simulator;

public sealed class MqttOptions
{
    public const string SectionName = "Mqtt";

    [Required]
    public string Host { get; set; } = string.Empty;

    [Range(1, 65535)]
    public int Port { get; set; } = 1883;

    public string? Username { get; set; }

    public string? Password { get; set; }

    [Required]
    public string TopicPrefix { get; set; } = "telemetry";
}

public sealed class SimulatorOptions
{
    public const string SectionName = "Simulator";

    /// <summary>How many devices to simulate. Ids are generated as "{DevicePrefix}-001", "-002", and so on.</summary>
    [Range(1, 1000)]
    public int DeviceCount { get; set; } = 5;

    /// <summary>Delay between publishing rounds. Each round sends one reading per sensor per device.</summary>
    [Range(10, 3_600_000)]
    public int IntervalMs { get; set; } = 1000;

    [Required]
    public string DevicePrefix { get; set; } = "pump";
}
