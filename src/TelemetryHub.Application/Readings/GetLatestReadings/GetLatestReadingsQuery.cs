using FluentValidation;
using MediatR;
using TelemetryHub.Application.Abstractions;
using TelemetryHub.Domain.Devices;
using TelemetryHub.Domain.Readings;

namespace TelemetryHub.Application.Readings.GetLatestReadings;

public sealed record GetLatestReadingsQuery(string DeviceId) : IRequest<IReadOnlyList<LatestReadingDto>>;

public sealed record LatestReadingDto(string SensorType, double Value, string Unit, DateTimeOffset Timestamp);

public sealed class GetLatestReadingsQueryValidator : AbstractValidator<GetLatestReadingsQuery>
{
    public GetLatestReadingsQueryValidator()
    {
        RuleFor(query => query.DeviceId).Must(DeviceId.IsValid).WithMessage("Device id is not valid.");
    }
}

public sealed class GetLatestReadingsQueryHandler(IReadingQueries readings)
    : IRequestHandler<GetLatestReadingsQuery, IReadOnlyList<LatestReadingDto>>
{
    public async Task<IReadOnlyList<LatestReadingDto>> Handle(
        GetLatestReadingsQuery request,
        CancellationToken cancellationToken)
    {
        var latest = await readings.GetLatestAsync(request.DeviceId, cancellationToken);

        return
        [
            .. latest
                .OrderBy(reading => reading.SensorType)
                .Select(reading => new LatestReadingDto(
                    reading.SensorType.ToSlug(),
                    reading.Value,
                    reading.Unit,
                    reading.Timestamp)),
        ];
    }
}
