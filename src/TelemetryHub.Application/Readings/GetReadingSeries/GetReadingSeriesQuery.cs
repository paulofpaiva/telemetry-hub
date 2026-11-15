using FluentValidation;
using MediatR;
using TelemetryHub.Application.Abstractions;
using TelemetryHub.Domain.Devices;
using TelemetryHub.Domain.Readings;

namespace TelemetryHub.Application.Readings.GetReadingSeries;

/// <summary>
/// Aggregated time series for one sensor. <paramref name="From"/> and <paramref name="To"/>
/// default to the last hour, <paramref name="Interval"/> to one minute.
/// </summary>
public sealed record GetReadingSeriesQuery(
    string DeviceId,
    string SensorType,
    DateTimeOffset? From = null,
    DateTimeOffset? To = null,
    string? Interval = null) : IRequest<ReadingSeriesDto>;

public sealed record ReadingSeriesDto(
    string DeviceId,
    string SensorType,
    DateTimeOffset From,
    DateTimeOffset To,
    string Interval,
    IReadOnlyList<ReadingWindow> Points);

public sealed class GetReadingSeriesQueryValidator : AbstractValidator<GetReadingSeriesQuery>
{
    /// <summary>Upper bound on windows per response, so a tiny interval over a long range cannot flood the API.</summary>
    public const int MaxPoints = 5000;

    public GetReadingSeriesQueryValidator(TimeProvider timeProvider)
    {
        RuleFor(query => query.DeviceId).Must(DeviceId.IsValid).WithMessage("Device id is not valid.");

        RuleFor(query => query.SensorType)
            .Must(value => SensorTypeExtensions.TryParseSlug(value, out _))
            .WithMessage("Sensor type must be one of: pressure, temperature, vibration.");

        RuleFor(query => query.Interval)
            .Must(value => value is null || SampleInterval.TryParse(value, out _))
            .WithMessage("Interval must be a number followed by s, m, h or d, for example '30s' or '5m'.");

        RuleFor(query => query)
            .Must(query =>
            {
                var (from, to, _) = GetReadingSeriesQueryHandler.ResolveRange(query, timeProvider.GetUtcNow());
                return from < to;
            })
            .WithName("From")
            .WithMessage("'from' must be earlier than 'to'.");

        RuleFor(query => query)
            .Must(query =>
            {
                var (from, to, interval) = GetReadingSeriesQueryHandler.ResolveRange(query, timeProvider.GetUtcNow());
                return to <= from || (to - from) / interval.Duration <= MaxPoints;
            })
            .When(query => query.Interval is null || SampleInterval.TryParse(query.Interval, out _))
            .WithName("Interval")
            .WithMessage($"The range would return more than {MaxPoints} points. Use a larger interval or a shorter range.");
    }
}

public sealed class GetReadingSeriesQueryHandler(IReadingQueries readings, TimeProvider timeProvider)
    : IRequestHandler<GetReadingSeriesQuery, ReadingSeriesDto>
{
    private static readonly TimeSpan DefaultRange = TimeSpan.FromHours(1);

    public async Task<ReadingSeriesDto> Handle(GetReadingSeriesQuery request, CancellationToken cancellationToken)
    {
        var (from, to, interval) = ResolveRange(request, timeProvider.GetUtcNow());
        SensorTypeExtensions.TryParseSlug(request.SensorType, out var sensorType);

        var points = await readings.GetSeriesAsync(request.DeviceId, sensorType, from, to, interval, cancellationToken);

        return new ReadingSeriesDto(request.DeviceId, sensorType.ToSlug(), from, to, interval.ToString(), points);
    }

    internal static (DateTimeOffset From, DateTimeOffset To, SampleInterval Interval) ResolveRange(
        GetReadingSeriesQuery query,
        DateTimeOffset now)
    {
        var to = (query.To ?? now).ToUniversalTime();
        var from = (query.From ?? to - DefaultRange).ToUniversalTime();

        if (!SampleInterval.TryParse(query.Interval ?? "1m", out var interval))
        {
            SampleInterval.TryParse("1m", out interval);
        }

        return (from, to, interval);
    }
}
