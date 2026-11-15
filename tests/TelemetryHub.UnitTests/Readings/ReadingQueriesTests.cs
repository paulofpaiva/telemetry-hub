using TelemetryHub.Application.Abstractions;
using TelemetryHub.Application.Readings.GetLatestReadings;
using TelemetryHub.Application.Readings.GetReadingSeries;
using TelemetryHub.Domain.Readings;
using TelemetryHub.UnitTests.Fakes;

namespace TelemetryHub.UnitTests.Readings;

public class ReadingQueriesTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 31, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Latest_returns_one_entry_per_sensor_for_the_requested_device()
    {
        var queries = new FakeReadingQueries();
        queries.Latest.Add(new Reading("pump-001", SensorType.Vibration, 2.4, "mm/s", Now));
        queries.Latest.Add(new Reading("pump-001", SensorType.Pressure, 8.1, "bar", Now));
        queries.Latest.Add(new Reading("pump-002", SensorType.Pressure, 9.9, "bar", Now));

        var result = await new GetLatestReadingsQueryHandler(queries)
            .Handle(new GetLatestReadingsQuery("pump-001"), CancellationToken.None);

        Assert.Equal(["pressure", "vibration"], result.Select(item => item.SensorType));
        Assert.Equal(8.1, result[0].Value);
    }

    [Fact]
    public async Task Series_defaults_to_the_last_hour_in_one_minute_windows()
    {
        var queries = new FakeReadingQueries();
        queries.Series.Add(new ReadingWindow(Now.AddMinutes(-1), 8, 7, 9, 60));
        var handler = new GetReadingSeriesQueryHandler(queries, new FixedTimeProvider(Now));

        var result = await handler.Handle(new GetReadingSeriesQuery("pump-001", "pressure"), CancellationToken.None);

        var request = Assert.NotNull(queries.LastSeriesRequest);
        Assert.Equal(Now.AddHours(-1), request.From);
        Assert.Equal(Now, request.To);
        Assert.Equal("1m", request.Interval.ToString());
        Assert.Equal(SensorType.Pressure, request.SensorType);
        Assert.Equal("1m", result.Interval);
        Assert.Single(result.Points);
    }

    [Fact]
    public async Task Series_passes_an_explicit_range_and_interval_through()
    {
        var queries = new FakeReadingQueries();
        var handler = new GetReadingSeriesQueryHandler(queries, new FixedTimeProvider(Now));
        var from = Now.AddDays(-2);
        var to = Now.AddDays(-1);

        await handler.Handle(new GetReadingSeriesQuery("pump-001", "temperature", from, to, "15m"), CancellationToken.None);

        var request = Assert.NotNull(queries.LastSeriesRequest);
        Assert.Equal((from, to), (request.From, request.To));
        Assert.Equal(TimeSpan.FromMinutes(15), request.Interval.Duration);
    }

    [Theory]
    [InlineData("humidity", "1m")]
    [InlineData("pressure", "1x")]
    [InlineData("pressure", "1m; DROP TABLE readings")]
    public void Series_validator_rejects_unknown_sensor_types_and_malformed_intervals(string sensorType, string interval)
    {
        var validator = new GetReadingSeriesQueryValidator(new FixedTimeProvider(Now));

        Assert.False(validator.Validate(new GetReadingSeriesQuery("pump-001", sensorType, Interval: interval)).IsValid);
    }

    [Fact]
    public void Series_validator_rejects_ranges_that_would_return_too_many_points()
    {
        var validator = new GetReadingSeriesQueryValidator(new FixedTimeProvider(Now));

        var tooMany = new GetReadingSeriesQuery("pump-001", "pressure", Now.AddDays(-1), Now, "1s");
        var acceptable = new GetReadingSeriesQuery("pump-001", "pressure", Now.AddDays(-1), Now, "1m");

        Assert.False(validator.Validate(tooMany).IsValid);
        Assert.True(validator.Validate(acceptable).IsValid);
    }

    [Fact]
    public void Series_validator_rejects_an_inverted_range()
    {
        var validator = new GetReadingSeriesQueryValidator(new FixedTimeProvider(Now));

        Assert.False(validator.Validate(new GetReadingSeriesQuery("pump-001", "pressure", Now, Now.AddHours(-1))).IsValid);
    }

    [Theory]
    [InlineData("30s", 30)]
    [InlineData("5m", 300)]
    [InlineData("2h", 7200)]
    [InlineData("1d", 86400)]
    public void SampleInterval_parses_amount_and_unit(string value, int expectedSeconds)
    {
        Assert.True(SampleInterval.TryParse(value, out var interval));
        Assert.Equal(TimeSpan.FromSeconds(expectedSeconds), interval.Duration);
        Assert.Equal(value, interval.ToString());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("m")]
    [InlineData("0m")]
    [InlineData("-5m")]
    [InlineData("5 m")]
    [InlineData("5M")]
    [InlineData("1.5h")]
    [InlineData("99999s")]
    public void SampleInterval_rejects_anything_else(string? value)
    {
        Assert.False(SampleInterval.TryParse(value, out _));
    }
}
