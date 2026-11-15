using TelemetryHub.Application.Readings.IngestReading;
using TelemetryHub.Domain.Readings;
using TelemetryHub.UnitTests.Fakes;

namespace TelemetryHub.UnitTests.Readings;

public class IngestReadingTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 31, 12, 0, 0, TimeSpan.Zero);

    private static IngestReadingCommand ValidCommand() =>
        new("pump-001", SensorType.Pressure, 8.2, "bar", Now.AddSeconds(-1));

    [Fact]
    public async Task Handler_writes_the_reading_normalized_to_utc()
    {
        var writer = new FakeReadingWriter();
        var handler = new IngestReadingCommandHandler(writer);
        var localTimestamp = new DateTimeOffset(2026, 1, 31, 9, 0, 0, TimeSpan.FromHours(-3));

        await handler.Handle(
            new IngestReadingCommand("pump-001", SensorType.Temperature, 64.5, " degC ", localTimestamp),
            CancellationToken.None);

        var reading = Assert.Single(writer.Written);
        Assert.Equal("pump-001", reading.DeviceId);
        Assert.Equal(SensorType.Temperature, reading.SensorType);
        Assert.Equal(64.5, reading.Value);
        Assert.Equal("degC", reading.Unit);
        Assert.Equal(TimeSpan.Zero, reading.Timestamp.Offset);
        Assert.Equal(localTimestamp, reading.Timestamp);
    }

    [Fact]
    public void Validator_accepts_a_well_formed_reading()
    {
        var validator = new IngestReadingCommandValidator(new FixedTimeProvider(Now));

        Assert.True(validator.Validate(ValidCommand()).IsValid);
    }

    [Theory]
    [InlineData("")]
    [InlineData("Pump-001")]
    [InlineData("pump 001")]
    [InlineData("pump/001")]
    [InlineData("-pump")]
    public void Validator_rejects_device_ids_unsafe_for_topics_and_keys(string deviceId)
    {
        var validator = new IngestReadingCommandValidator(new FixedTimeProvider(Now));

        var result = validator.Validate(ValidCommand() with { DeviceId = deviceId });

        Assert.Contains(result.Errors, error => error.PropertyName == nameof(IngestReadingCommand.DeviceId));
    }

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    public void Validator_rejects_non_finite_values(double value)
    {
        var validator = new IngestReadingCommandValidator(new FixedTimeProvider(Now));

        var result = validator.Validate(ValidCommand() with { Value = value });

        Assert.Contains(result.Errors, error => error.PropertyName == nameof(IngestReadingCommand.Value));
    }

    [Fact]
    public void Validator_tolerates_small_clock_skew_but_rejects_far_future_timestamps()
    {
        var validator = new IngestReadingCommandValidator(new FixedTimeProvider(Now));

        Assert.True(validator.Validate(ValidCommand() with { Timestamp = Now.AddMinutes(4) }).IsValid);
        Assert.False(validator.Validate(ValidCommand() with { Timestamp = Now.AddMinutes(6) }).IsValid);
    }

    [Theory]
    [InlineData("")]
    [InlineData("a-unit-name-that-is-way-too-long")]
    public void Validator_rejects_missing_or_oversized_units(string unit)
    {
        var validator = new IngestReadingCommandValidator(new FixedTimeProvider(Now));

        Assert.False(validator.Validate(ValidCommand() with { Unit = unit }).IsValid);
    }
}
