using FluentValidation;
using TelemetryHub.Domain.Devices;

namespace TelemetryHub.Application.Readings.IngestReading;

public sealed class IngestReadingCommandValidator : AbstractValidator<IngestReadingCommand>
{
    public const int UnitMaxLength = 16;

    /// <summary>Tolerance for device clocks that run slightly ahead of the server.</summary>
    public static readonly TimeSpan MaxClockSkew = TimeSpan.FromMinutes(5);

    public IngestReadingCommandValidator(TimeProvider timeProvider)
    {
        RuleFor(command => command.DeviceId)
            .Must(DeviceId.IsValid)
            .WithMessage($"Device id must be 1 to {DeviceId.MaxLength} lowercase letters, digits, '-' or '_'.");

        RuleFor(command => command.SensorType).IsInEnum();

        RuleFor(command => command.Value)
            .Must(double.IsFinite)
            .WithMessage("Value must be a finite number.");

        RuleFor(command => command.Unit).NotEmpty().MaximumLength(UnitMaxLength);

        RuleFor(command => command.Timestamp)
            .NotEqual(default(DateTimeOffset))
            .Must(timestamp => timestamp <= timeProvider.GetUtcNow() + MaxClockSkew)
            .WithMessage("Timestamp is too far in the future.");
    }
}
