using MediatR;
using TelemetryHub.Application.Abstractions;
using TelemetryHub.Application.Exceptions;
using TelemetryHub.Domain.Devices;

namespace TelemetryHub.Application.Devices.DeleteDevice;

public sealed record DeleteDeviceCommand(string DeviceId) : IRequest;

public sealed class DeleteDeviceCommandHandler(IDeviceRepository devices) : IRequestHandler<DeleteDeviceCommand>
{
    public async Task Handle(DeleteDeviceCommand request, CancellationToken cancellationToken)
    {
        var device = await devices.GetByDeviceIdAsync(request.DeviceId, cancellationToken)
            ?? throw new NotFoundException(nameof(Device), request.DeviceId);

        devices.Remove(device);
        await devices.SaveChangesAsync(cancellationToken);
    }
}
