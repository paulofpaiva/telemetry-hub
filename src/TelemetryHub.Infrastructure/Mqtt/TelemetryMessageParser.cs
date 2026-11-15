using System.Buffers;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using TelemetryHub.Application.Readings.IngestReading;
using TelemetryHub.Domain.Readings;

namespace TelemetryHub.Infrastructure.Mqtt;

/// <summary>
/// Turns an MQTT message into an <see cref="IngestReadingCommand"/>. Only the shape is checked
/// here (topic layout, JSON); business rules live in the command validator.
/// </summary>
public static class TelemetryMessageParser
{
    public static bool TryParse(
        string topicPrefix,
        string topic,
        ReadOnlySequence<byte> payload,
        DateTimeOffset receivedAt,
        [NotNullWhen(true)] out IngestReadingCommand? command,
        [NotNullWhen(false)] out string? error)
    {
        command = null;

        // Expected layout: {prefix}/{deviceId}/{sensorType}
        var segments = topic.Split('/');
        if (segments.Length != 3 || segments[0] != topicPrefix)
        {
            error = "Topic does not match '{prefix}/{deviceId}/{sensorType}'.";
            return false;
        }

        if (!SensorTypeExtensions.TryParseSlug(segments[2], out var sensorType))
        {
            error = $"Unknown sensor type '{segments[2]}'.";
            return false;
        }

        ReadingPayload? body;
        try
        {
            var reader = new Utf8JsonReader(payload);
            body = JsonSerializer.Deserialize<ReadingPayload>(ref reader, JsonSerializerOptions.Web);
        }
        catch (JsonException)
        {
            error = "Payload is not valid JSON.";
            return false;
        }

        if (body?.Value is null)
        {
            error = "Payload is missing 'value'.";
            return false;
        }

        command = new IngestReadingCommand(
            segments[1],
            sensorType,
            body.Value.Value,
            body.Unit ?? string.Empty,
            // Devices without a clock may omit the timestamp; fall back to arrival time.
            body.Timestamp ?? receivedAt);
        error = null;
        return true;
    }

    private sealed record ReadingPayload(double? Value, string? Unit, DateTimeOffset? Timestamp);
}
