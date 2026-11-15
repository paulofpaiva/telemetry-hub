using System.Text.RegularExpressions;

namespace TelemetryHub.Domain.Devices;

/// <summary>
/// Rules for the public device identifier. It travels inside MQTT topics and S3 keys,
/// so it is restricted to characters that are safe in both.
/// </summary>
public static partial class DeviceId
{
    public const int MaxLength = 64;

    public static bool IsValid(string? value) =>
        !string.IsNullOrEmpty(value) && value.Length <= MaxLength && Pattern().IsMatch(value);

    [GeneratedRegex("^[a-z0-9][a-z0-9_-]*$")]
    private static partial Regex Pattern();
}
