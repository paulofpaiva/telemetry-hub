using MediatR;
using TelemetryHub.Application.Abstractions;
using TelemetryHub.Application.Exceptions;
using TelemetryHub.Domain.Devices;

namespace TelemetryHub.Application.Devices.GetDevice;

public sealed record GetDeviceQuery(string DeviceId) : IRequest<DeviceDto>;

public sealed class GetDeviceQueryHandler(IDeviceRepository devices) : IRequestHandler<GetDeviceQuery, DeviceDto>
{
    public async Task<DeviceDto> Handle(GetDeviceQuery request, CancellationToken cancellationToken)
    {
        var device = await devices.GetByDeviceIdAsync(request.DeviceId, cancellationToken)
            ?? throw new NotFoundException(nameof(Device), request.DeviceId);

        return DeviceDto.From(device);
    }
}
