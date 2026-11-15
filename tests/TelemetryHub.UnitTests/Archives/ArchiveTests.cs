using System.Text.Json;
using TelemetryHub.Application.Abstractions;
using TelemetryHub.Application.Archives;
using TelemetryHub.Application.Exceptions;
using TelemetryHub.UnitTests.Fakes;

namespace TelemetryHub.UnitTests.Archives;

public class ArchiveTests
{
    private static readonly DateTimeOffset WindowStart = new(2026, 1, 31, 10, 15, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset WindowEnd = new(2026, 1, 31, 10, 20, 0, TimeSpan.Zero);

    private readonly FakeReadingQueries _queries = new();
    private readonly InMemoryArchiveStorage _storage = new();
    private readonly FixedTimeProvider _clock = new(WindowEnd.AddSeconds(15));

    [Fact]
    public async Task Archive_writes_one_file_per_device_with_its_sensor_aggregates()
    {
        _queries.Aggregates.Add(new DeviceSensorAggregate("pump-002", "pressure", "bar", 8.5, 8.0, 9.0, 300));
        _queries.Aggregates.Add(new DeviceSensorAggregate("pump-001", "vibration", "mm/s", 2.5, 2.0, 6.0, 300));
        _queries.Aggregates.Add(new DeviceSensorAggregate("pump-001", "pressure", "bar", 8.1, 7.9, 8.4, 300));
        var handler = new ArchiveReadingsCommandHandler(_queries, _storage, _clock);

        var result = await handler.Handle(new ArchiveReadingsCommand(WindowStart, WindowEnd), CancellationToken.None);

        Assert.Equal((WindowStart, WindowEnd), _queries.LastAggregatesRequest);
        Assert.Equal(
            [
                "readings/date=2026-01-31/device=pump-001/20260131T101500Z_20260131T102000Z.json",
                "readings/date=2026-01-31/device=pump-002/20260131T101500Z_20260131T102000Z.json",
            ],
            result.Keys);
        Assert.Equal(result.Keys, _storage.Objects.Keys);

        var (content, contentType) = _storage.Objects[result.Keys[0]];
        var document = JsonSerializer.Deserialize<DeviceArchiveDocument>(content, JsonSerializerOptions.Web)!;
        Assert.Equal("application/json", contentType);
        Assert.Equal("pump-001", document.DeviceId);
        Assert.Equal(WindowStart, document.WindowStart);
        Assert.Equal(WindowEnd, document.WindowEnd);
        Assert.Equal(["pressure", "vibration"], document.Sensors.Select(sensor => sensor.SensorType));
        Assert.Equal(6.0, document.Sensors[1].Max);
    }

    [Fact]
    public async Task Archive_writes_nothing_for_a_window_without_readings()
    {
        var handler = new ArchiveReadingsCommandHandler(_queries, _storage, _clock);

        var result = await handler.Handle(new ArchiveReadingsCommand(WindowStart, WindowEnd), CancellationToken.None);

        Assert.Empty(result.Keys);
        Assert.Empty(_storage.Objects);
    }

    [Fact]
    public async Task Archive_is_idempotent_for_the_same_window()
    {
        _queries.Aggregates.Add(new DeviceSensorAggregate("pump-001", "pressure", "bar", 8.1, 7.9, 8.4, 300));
        var handler = new ArchiveReadingsCommandHandler(_queries, _storage, _clock);

        await handler.Handle(new ArchiveReadingsCommand(WindowStart, WindowEnd), CancellationToken.None);
        await handler.Handle(new ArchiveReadingsCommand(WindowStart, WindowEnd), CancellationToken.None);

        Assert.Single(_storage.Objects);
    }

    [Fact]
    public void Archive_validator_rejects_inverted_and_oversized_windows()
    {
        var validator = new ArchiveReadingsCommandValidator();

        Assert.False(validator.Validate(new ArchiveReadingsCommand(WindowEnd, WindowStart)).IsValid);
        Assert.False(validator.Validate(new ArchiveReadingsCommand(WindowStart, WindowStart.AddDays(2))).IsValid);
        Assert.True(validator.Validate(new ArchiveReadingsCommand(WindowStart, WindowEnd)).IsValid);
    }

    [Fact]
    public async Task List_uses_the_narrowest_prefix_when_date_and_device_are_given()
    {
        await SeedAsync("pump-001", "pump-002");
        var handler = new ListArchivesQueryHandler(_storage);

        var result = await handler.Handle(
            new ListArchivesQuery(new DateOnly(2026, 1, 31), "pump-002", Limit: 10),
            CancellationToken.None);

        Assert.Equal(("readings/date=2026-01-31/device=pump-002/", 10), _storage.LastListRequest);
        Assert.Equal("readings/date=2026-01-31/device=pump-002/20260131T101500Z_20260131T102000Z.json", Assert.Single(result).Key);
    }

    [Fact]
    public async Task List_filters_by_device_across_dates_when_no_date_is_given()
    {
        await SeedAsync("pump-001", "pump-002", "pump-0020");
        var handler = new ListArchivesQueryHandler(_storage);

        var result = await handler.Handle(new ListArchivesQuery(DeviceId: "pump-002"), CancellationToken.None);

        Assert.Equal("readings/", _storage.LastListRequest?.Prefix);
        Assert.All(result, item => Assert.Contains("/device=pump-002/", item.Key));
        Assert.Single(result);
    }

    [Fact]
    public async Task Download_returns_the_content_or_not_found()
    {
        await SeedAsync("pump-001");
        var handler = new GetArchiveFileQueryHandler(_storage);
        var key = ArchiveKey.For("pump-001", WindowStart, WindowEnd);

        var file = await handler.Handle(new GetArchiveFileQuery(key), CancellationToken.None);
        await using (file.Content)
        {
            Assert.Equal("application/json", file.ContentType);
            Assert.True(file.SizeBytes > 0);
        }

        await Assert.ThrowsAsync<NotFoundException>(() =>
            handler.Handle(new GetArchiveFileQuery(ArchiveKey.For("ghost", WindowStart, WindowEnd)), CancellationToken.None));
    }

    [Theory]
    [InlineData("readings/date=2026-01-31/device=pump-001/20260131T101500Z_20260131T102000Z.json", true)]
    [InlineData("other/secret.txt", false)]
    [InlineData("readings/../other/secret.json", false)]
    [InlineData("readings/date=2026-01-31/device=pump-001/../../x.json", false)]
    [InlineData("readings/date=not-a-date/device=pump-001/20260131T101500Z_20260131T102000Z.json", false)]
    [InlineData("readings/date=2026-01-31/device=Bad Id/20260131T101500Z_20260131T102000Z.json", false)]
    [InlineData("readings/date=2026-01-31/device=pump-001/file.txt", false)]
    [InlineData("", false)]
    public void Key_validation_only_accepts_the_archive_layout(string key, bool expected)
    {
        Assert.Equal(expected, ArchiveKey.IsValid(key));
        Assert.Equal(expected, new GetArchiveFileQueryValidator().Validate(new GetArchiveFileQuery(key)).IsValid);
    }

    [Theory]
    [InlineData("2026-01-31T10:20:09Z", "2026-01-31T10:10:00Z", "2026-01-31T10:15:00Z")]
    [InlineData("2026-01-31T10:20:10Z", "2026-01-31T10:15:00Z", "2026-01-31T10:20:00Z")]
    [InlineData("2026-01-31T10:24:59Z", "2026-01-31T10:15:00Z", "2026-01-31T10:20:00Z")]
    [InlineData("2026-02-01T00:00:10Z", "2026-01-31T23:55:00Z", "2026-02-01T00:00:00Z")]
    public void Window_is_clock_aligned_and_only_complete_after_the_grace_period(string now, string expectedStart, string expectedEnd)
    {
        var (start, end) = ArchiveWindow.LastComplete(
            DateTimeOffset.Parse(now, System.Globalization.CultureInfo.InvariantCulture),
            TimeSpan.FromMinutes(5),
            TimeSpan.FromSeconds(10));

        Assert.Equal(DateTimeOffset.Parse(expectedStart, System.Globalization.CultureInfo.InvariantCulture), start);
        Assert.Equal(DateTimeOffset.Parse(expectedEnd, System.Globalization.CultureInfo.InvariantCulture), end);
    }

    private async Task SeedAsync(params string[] deviceIds)
    {
        foreach (var deviceId in deviceIds)
        {
            await _storage.PutAsync(ArchiveKey.For(deviceId, WindowStart, WindowEnd), "{}"u8.ToArray(), "application/json", CancellationToken.None);
        }
    }
}
