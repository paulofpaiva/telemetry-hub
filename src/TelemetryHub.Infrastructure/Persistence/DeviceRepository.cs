using Microsoft.EntityFrameworkCore;
using TelemetryHub.Application.Abstractions;
using TelemetryHub.Application.Exceptions;
using TelemetryHub.Domain.Devices;
using Npgsql;

namespace TelemetryHub.Infrastructure.Persistence;

internal sealed class DeviceRepository(AppDbContext dbContext) : IDeviceRepository
{
    public Task<Device?> GetByDeviceIdAsync(string deviceId, CancellationToken cancellationToken) =>
        dbContext.Devices.SingleOrDefaultAsync(device => device.DeviceId == deviceId, cancellationToken);

    public Task<bool> ExistsAsync(string deviceId, CancellationToken cancellationToken) =>
        dbContext.Devices.AnyAsync(device => device.DeviceId == deviceId, cancellationToken);

    public async Task<(IReadOnlyList<Device> Items, int TotalCount)> ListAsync(
        int page,
        int pageSize,
        bool? isActive,
        CancellationToken cancellationToken)
    {
        var query = dbContext.Devices.AsNoTracking();
        if (isActive is not null)
        {
            query = query.Where(device => device.IsActive == isActive);
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderBy(device => device.DeviceId)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return (items, totalCount);
    }

    public void Add(Device device) => dbContext.Devices.Add(device);

    public void Remove(Device device) => dbContext.Devices.Remove(device);

    public async Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception)
            when (exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            // Two concurrent creates can both pass the existence check; the unique index decides.
            throw new ConflictException("A device with the same device id already exists.");
        }
    }
}
