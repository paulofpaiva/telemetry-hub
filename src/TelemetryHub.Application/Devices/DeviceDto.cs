using TelemetryHub.Domain.Devices;

namespace TelemetryHub.Application.Devices;

public sealed record DeviceDto(
    Guid Id,
    string DeviceId,
    string Name,
    string? Location,
    bool IsActive,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt)
{
    public static DeviceDto From(Device device) => new(
        device.Id,
        device.DeviceId,
        device.Name,
        device.Location,
        device.IsActive,
        device.CreatedAt,
        device.UpdatedAt);
}

public sealed record PagedResult<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount);
