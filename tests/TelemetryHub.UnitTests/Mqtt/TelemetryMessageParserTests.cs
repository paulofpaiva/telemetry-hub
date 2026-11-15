using System.Buffers;
using System.Text;
using TelemetryHub.Domain.Readings;
using TelemetryHub.Infrastructure.Mqtt;

namespace TelemetryHub.UnitTests.Mqtt;

public class TelemetryMessageParserTests
{
    private static readonly DateTimeOffset ReceivedAt = new(2026, 1, 31, 12, 0, 0, TimeSpan.Zero);

    private static ReadOnlySequence<byte> Payload(string json) => new(Encoding.UTF8.GetBytes(json));

    [Fact]
    public void Parses_device_and_sensor_from_the_topic_and_values_from_the_payload()
    {
        var parsed = TelemetryMessageParser.TryParse(
            "telemetry",
            "telemetry/pump-001/vibration",
            Payload("""{"value":2.75,"unit":"mm/s","timestamp":"2026-01-31T11:59:58Z"}"""),
            ReceivedAt,
            out var command,
            out var error);

        Assert.True(parsed);
        Assert.Null(error);
        Assert.Equal("pump-001", command!.DeviceId);
        Assert.Equal(SensorType.Vibration, command.SensorType);
        Assert.Equal(2.75, command.Value);
        Assert.Equal("mm/s", command.Unit);
        Assert.Equal(new DateTimeOffset(2026, 1, 31, 11, 59, 58, TimeSpan.Zero), command.Timestamp);
    }

    [Fact]
    public void Falls_back_to_arrival_time_when_the_payload_has_no_timestamp()
    {
        var parsed = TelemetryMessageParser.TryParse(
            "telemetry",
            "telemetry/pump-001/pressure",
            Payload("""{"value":8.1,"unit":"bar"}"""),
            ReceivedAt,
            out var command,
            out _);

        Assert.True(parsed);
        Assert.Equal(ReceivedAt, command!.Timestamp);
    }

    [Theory]
    [InlineData("telemetry/pump-001", """{"value":1}""")]
    [InlineData("telemetry/pump-001/pressure/extra", """{"value":1}""")]
    [InlineData("other/pump-001/pressure", """{"value":1}""")]
    [InlineData("telemetry/pump-001/humidity", """{"value":1}""")]
    [InlineData("telemetry/pump-001/pressure", "not json")]
    [InlineData("telemetry/pump-001/pressure", "")]
    [InlineData("telemetry/pump-001/pressure", "null")]
    [InlineData("telemetry/pump-001/pressure", """{"unit":"bar"}""")]
    [InlineData("telemetry/pump-001/pressure", """{"value":"high"}""")]
    public void Rejects_malformed_topics_and_payloads_with_a_reason(string topic, string payload)
    {
        var parsed = TelemetryMessageParser.TryParse("telemetry", topic, Payload(payload), ReceivedAt, out var command, out var error);

        Assert.False(parsed);
        Assert.Null(command);
        Assert.False(string.IsNullOrWhiteSpace(error));
    }
}
