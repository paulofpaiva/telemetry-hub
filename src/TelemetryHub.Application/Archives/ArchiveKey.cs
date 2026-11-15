using System.Globalization;
using TelemetryHub.Domain.Devices;

namespace TelemetryHub.Application.Archives;

/// <summary>
/// Layout of archive object keys. Hive-style partitions keep the bucket easy to browse and
/// ready for query engines that understand "name=value" folders:
/// <c>readings/date=2026-01-31/device=pump-001/20260131T101500Z_20260131T102000Z.json</c>
/// </summary>
public static class ArchiveKey
{
    public const string Root = "readings/";

    private const string DateFormat = "yyyy-MM-dd";
    private const string StampFormat = "yyyyMMdd'T'HHmmss'Z'";

    public static string For(string deviceId, DateTimeOffset windowStart, DateTimeOffset windowEnd)
    {
        var start = windowStart.UtcDateTime;
        var end = windowEnd.UtcDateTime;

        return string.Create(
            CultureInfo.InvariantCulture,
            $"{Root}date={start.ToString(DateFormat, CultureInfo.InvariantCulture)}/device={deviceId}/{start.ToString(StampFormat, CultureInfo.InvariantCulture)}_{end.ToString(StampFormat, CultureInfo.InvariantCulture)}.json");
    }

    /// <summary>Narrowest prefix that still contains every key for the given filters.</summary>
    public static string PrefixFor(DateOnly? date, string? deviceId)
    {
        if (date is null)
        {
            return Root;
        }

        var datePrefix = $"{Root}date={date.Value.ToString(DateFormat, CultureInfo.InvariantCulture)}/";
        return deviceId is null ? datePrefix : $"{datePrefix}device={deviceId}/";
    }

    public static bool BelongsToDevice(string key, string deviceId) =>
        key.Contains($"/device={deviceId}/", StringComparison.Ordinal);

    /// <summary>True when the key has the exact shape produced by <see cref="For"/>.</summary>
    public static bool IsValid(string? key)
    {
        if (string.IsNullOrEmpty(key) || !key.StartsWith(Root, StringComparison.Ordinal))
        {
            return false;
        }

        var segments = key.Split('/');
        return segments.Length == 4
            && segments[1].StartsWith("date=", StringComparison.Ordinal)
            && DateOnly.TryParseExact(segments[1].AsSpan(5), DateFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out _)
            && segments[2].StartsWith("device=", StringComparison.Ordinal)
            && DeviceId.IsValid(segments[2][7..])
            && segments[3].EndsWith(".json", StringComparison.Ordinal)
            && segments[3].AsSpan(0, segments[3].Length - 5).IndexOfAnyExcept("0123456789TZ_") < 0;
    }
}
