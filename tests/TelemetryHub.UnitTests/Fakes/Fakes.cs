using TelemetryHub.Application.Abstractions;
using TelemetryHub.Domain.Devices;
using TelemetryHub.Domain.Readings;

namespace TelemetryHub.UnitTests.Fakes;

/// <summary>Clock frozen at a known instant.</summary>
internal sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
{
    public DateTimeOffset Now { get; set; } = now;

    public override DateTimeOffset GetUtcNow() => Now;
}

internal sealed class FakeReadingWriter : IReadingWriter
{
    public List<Reading> Written { get; } = [];

    public int Flushes { get; private set; }

    public ValueTask WriteAsync(Reading reading, CancellationToken cancellationToken)
    {
        Written.Add(reading);
        return ValueTask.CompletedTask;
    }

    public ValueTask FlushAsync(CancellationToken cancellationToken)
    {
        Flushes++;
        return ValueTask.CompletedTask;
    }
}

internal sealed class FakeReadingQueries : IReadingQueries
{
    public List<Reading> Latest { get; } = [];

    public List<ReadingWindow> Series { get; } = [];

    public List<DeviceSensorAggregate> Aggregates { get; } = [];

    public (string DeviceId, SensorType SensorType, DateTimeOffset From, DateTimeOffset To, SampleInterval Interval)? LastSeriesRequest { get; private set; }

    public (DateTimeOffset From, DateTimeOffset To)? LastAggregatesRequest { get; private set; }

    public Task<IReadOnlyList<Reading>> GetLatestAsync(string deviceId, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<Reading>>([.. Latest.Where(reading => reading.DeviceId == deviceId)]);

    public Task<IReadOnlyList<ReadingWindow>> GetSeriesAsync(
        string deviceId,
        SensorType sensorType,
        DateTimeOffset from,
        DateTimeOffset to,
        SampleInterval interval,
        CancellationToken cancellationToken)
    {
        LastSeriesRequest = (deviceId, sensorType, from, to, interval);
        return Task.FromResult<IReadOnlyList<ReadingWindow>>(Series);
    }

    public Task<IReadOnlyList<DeviceSensorAggregate>> GetAggregatesAsync(
        DateTimeOffset from,
        DateTimeOffset to,
        CancellationToken cancellationToken)
    {
        LastAggregatesRequest = (from, to);
        return Task.FromResult<IReadOnlyList<DeviceSensorAggregate>>(Aggregates);
    }
}

internal sealed class InMemoryArchiveStorage : IArchiveStorage
{
    public SortedDictionary<string, (byte[] Content, string ContentType)> Objects { get; } = new(StringComparer.Ordinal);

    public (string Prefix, int MaxResults)? LastListRequest { get; private set; }

    public Task PutAsync(string key, ReadOnlyMemory<byte> content, string contentType, CancellationToken cancellationToken)
    {
        Objects[key] = (content.ToArray(), contentType);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<ArchiveObject>> ListAsync(string prefix, int maxResults, CancellationToken cancellationToken)
    {
        LastListRequest = (prefix, maxResults);
        return Task.FromResult<IReadOnlyList<ArchiveObject>>(
        [
            .. Objects
                .Where(item => item.Key.StartsWith(prefix, StringComparison.Ordinal))
                .Take(maxResults)
                .Select(item => new ArchiveObject(item.Key, item.Value.Content.Length, DateTimeOffset.UnixEpoch)),
        ]);
    }

    public Task<ArchiveContent?> OpenReadAsync(string key, CancellationToken cancellationToken) =>
        Task.FromResult(Objects.TryGetValue(key, out var item)
            ? new ArchiveContent(new MemoryStream(item.Content), item.ContentType, item.Content.Length)
            : null);
}

internal sealed class InMemoryDeviceRepository : IDeviceRepository
{
    private readonly List<Device> _devices = [];

    public int SaveCount { get; private set; }

    public IReadOnlyList<Device> Devices => _devices;

    public Task<Device?> GetByDeviceIdAsync(string deviceId, CancellationToken cancellationToken) =>
        Task.FromResult(_devices.SingleOrDefault(device => device.DeviceId == deviceId));

    public Task<bool> ExistsAsync(string deviceId, CancellationToken cancellationToken) =>
        Task.FromResult(_devices.Any(device => device.DeviceId == deviceId));

    public Task<(IReadOnlyList<Device> Items, int TotalCount)> ListAsync(
        int page,
        int pageSize,
        bool? isActive,
        CancellationToken cancellationToken)
    {
        var filtered = _devices
            .Where(device => isActive is null || device.IsActive == isActive)
            .OrderBy(device => device.DeviceId, StringComparer.Ordinal)
            .ToList();

        IReadOnlyList<Device> items = [.. filtered.Skip((page - 1) * pageSize).Take(pageSize)];
        return Task.FromResult((items, filtered.Count));
    }

    public void Add(Device device) => _devices.Add(device);

    public void Remove(Device device) => _devices.Remove(device);

    public Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        SaveCount++;
        return Task.CompletedTask;
    }
}
