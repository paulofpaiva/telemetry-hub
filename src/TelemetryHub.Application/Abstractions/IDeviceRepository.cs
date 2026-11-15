using TelemetryHub.Domain.Devices;

namespace TelemetryHub.Application.Abstractions;

public interface IDeviceRepository
{
    Task<Device?> GetByDeviceIdAsync(string deviceId, CancellationToken cancellationToken);

    Task<bool> ExistsAsync(string deviceId, CancellationToken cancellationToken);

    Task<(IReadOnlyList<Device> Items, int TotalCount)> ListAsync(
        int page,
        int pageSize,
        bool? isActive,
        CancellationToken cancellationToken);

    void Add(Device device);

    void Remove(Device device);

    Task SaveChangesAsync(CancellationToken cancellationToken);
}
