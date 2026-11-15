namespace TelemetryHub.Domain.Devices;

/// <summary>A piece of equipment that reports telemetry, such as a pump or a compressor.</summary>
public sealed class Device
{
    public const int NameMaxLength = 120;
    public const int LocationMaxLength = 120;

    private Device()
    {
    }

    public Guid Id { get; private set; }

    /// <summary>Public identifier used in MQTT topics, for example "pump-001".</summary>
    public string DeviceId { get; private set; } = string.Empty;

    public string Name { get; private set; } = string.Empty;

    public string? Location { get; private set; }

    public bool IsActive { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public static Device Create(string deviceId, string name, string? location, DateTimeOffset now)
    {
        if (!Devices.DeviceId.IsValid(deviceId))
        {
            throw new ArgumentException("Device id must be lowercase letters, digits, '-' or '_'.", nameof(deviceId));
        }

        var device = new Device
        {
            Id = Guid.CreateVersion7(now),
            DeviceId = deviceId,
            IsActive = true,
            CreatedAt = now,
        };
        device.Update(name, location, isActive: true, now);
        return device;
    }

    public void Update(string name, string? location, bool isActive, DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Device name is required.", nameof(name));
        }

        Name = name.Trim();
        Location = string.IsNullOrWhiteSpace(location) ? null : location.Trim();
        IsActive = isActive;
        UpdatedAt = now;
    }
}
