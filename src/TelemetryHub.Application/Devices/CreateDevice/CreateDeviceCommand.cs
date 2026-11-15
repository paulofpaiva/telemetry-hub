using FluentValidation;
using MediatR;
using TelemetryHub.Application.Abstractions;
using TelemetryHub.Application.Exceptions;
using TelemetryHub.Domain.Devices;

namespace TelemetryHub.Application.Devices.CreateDevice;

public sealed record CreateDeviceCommand(string DeviceId, string Name, string? Location) : IRequest<DeviceDto>;

public sealed class CreateDeviceCommandValidator : AbstractValidator<CreateDeviceCommand>
{
    public CreateDeviceCommandValidator()
    {
        RuleFor(command => command.DeviceId)
            .Must(DeviceId.IsValid)
            .WithMessage($"Device id must be 1 to {DeviceId.MaxLength} lowercase letters, digits, '-' or '_'.");

        RuleFor(command => command.Name).NotEmpty().MaximumLength(Device.NameMaxLength);
        RuleFor(command => command.Location).MaximumLength(Device.LocationMaxLength);
    }
}

public sealed class CreateDeviceCommandHandler(IDeviceRepository devices, TimeProvider timeProvider)
    : IRequestHandler<CreateDeviceCommand, DeviceDto>
{
    public async Task<DeviceDto> Handle(CreateDeviceCommand request, CancellationToken cancellationToken)
    {
        if (await devices.ExistsAsync(request.DeviceId, cancellationToken))
        {
            throw new ConflictException($"Device '{request.DeviceId}' already exists.");
        }

        var device = Device.Create(request.DeviceId, request.Name, request.Location, timeProvider.GetUtcNow());
        devices.Add(device);
        await devices.SaveChangesAsync(cancellationToken);

        return DeviceDto.From(device);
    }
}
