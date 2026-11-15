using FluentValidation;
using MediatR;
using TelemetryHub.Application.Abstractions;
using TelemetryHub.Application.Exceptions;
using TelemetryHub.Domain.Devices;

namespace TelemetryHub.Application.Devices.UpdateDevice;

public sealed record UpdateDeviceCommand(string DeviceId, string Name, string? Location, bool IsActive)
    : IRequest<DeviceDto>;

public sealed class UpdateDeviceCommandValidator : AbstractValidator<UpdateDeviceCommand>
{
    public UpdateDeviceCommandValidator()
    {
        RuleFor(command => command.DeviceId).NotEmpty();
        RuleFor(command => command.Name).NotEmpty().MaximumLength(Device.NameMaxLength);
        RuleFor(command => command.Location).MaximumLength(Device.LocationMaxLength);
    }
}

public sealed class UpdateDeviceCommandHandler(IDeviceRepository devices, TimeProvider timeProvider)
    : IRequestHandler<UpdateDeviceCommand, DeviceDto>
{
    public async Task<DeviceDto> Handle(UpdateDeviceCommand request, CancellationToken cancellationToken)
    {
        var device = await devices.GetByDeviceIdAsync(request.DeviceId, cancellationToken)
            ?? throw new NotFoundException(nameof(Device), request.DeviceId);

        device.Update(request.Name, request.Location, request.IsActive, timeProvider.GetUtcNow());
        await devices.SaveChangesAsync(cancellationToken);

        return DeviceDto.From(device);
    }
}
