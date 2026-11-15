using System.Text.Json;
using FluentValidation;
using MediatR;
using TelemetryHub.Application.Abstractions;

namespace TelemetryHub.Application.Archives;

/// <summary>
/// Aggregates every reading in [<paramref name="WindowStart"/>, <paramref name="WindowEnd"/>)
/// and stores one file per device. Keys are derived from the window, so running the same
/// window twice overwrites the same objects instead of duplicating them.
/// </summary>
public sealed record ArchiveReadingsCommand(DateTimeOffset WindowStart, DateTimeOffset WindowEnd)
    : IRequest<ArchiveReadingsResult>;

public sealed record ArchiveReadingsResult(DateTimeOffset WindowStart, DateTimeOffset WindowEnd, IReadOnlyList<string> Keys);

/// <summary>Content of one archive file.</summary>
public sealed record DeviceArchiveDocument(
    string DeviceId,
    DateTimeOffset WindowStart,
    DateTimeOffset WindowEnd,
    DateTimeOffset GeneratedAt,
    IReadOnlyList<SensorArchiveEntry> Sensors);

public sealed record SensorArchiveEntry(string SensorType, string Unit, double Avg, double Min, double Max, long Count);

public sealed class ArchiveReadingsCommandValidator : AbstractValidator<ArchiveReadingsCommand>
{
    public static readonly TimeSpan MaxWindow = TimeSpan.FromDays(1);

    public ArchiveReadingsCommandValidator()
    {
        RuleFor(command => command.WindowEnd)
            .GreaterThan(command => command.WindowStart)
            .WithMessage("Window end must be after window start.");

        RuleFor(command => command)
            .Must(command => command.WindowEnd - command.WindowStart <= MaxWindow)
            .WithName("Window")
            .WithMessage("Window cannot be longer than one day.");
    }
}

public sealed class ArchiveReadingsCommandHandler(
    IReadingQueries readings,
    IArchiveStorage storage,
    TimeProvider timeProvider) : IRequestHandler<ArchiveReadingsCommand, ArchiveReadingsResult>
{
    public const string ContentType = "application/json";

    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    public async Task<ArchiveReadingsResult> Handle(ArchiveReadingsCommand request, CancellationToken cancellationToken)
    {
        var windowStart = request.WindowStart.ToUniversalTime();
        var windowEnd = request.WindowEnd.ToUniversalTime();

        var aggregates = await readings.GetAggregatesAsync(windowStart, windowEnd, cancellationToken);
        var generatedAt = timeProvider.GetUtcNow();
        var keys = new List<string>();

        foreach (var device in aggregates.GroupBy(aggregate => aggregate.DeviceId).OrderBy(group => group.Key, StringComparer.Ordinal))
        {
            var document = new DeviceArchiveDocument(
                device.Key,
                windowStart,
                windowEnd,
                generatedAt,
                [
                    .. device
                        .OrderBy(aggregate => aggregate.SensorType, StringComparer.Ordinal)
                        .Select(aggregate => new SensorArchiveEntry(
                            aggregate.SensorType,
                            aggregate.Unit,
                            aggregate.Avg,
                            aggregate.Min,
                            aggregate.Max,
                            aggregate.Count)),
                ]);

            var key = ArchiveKey.For(device.Key, windowStart, windowEnd);
            await storage.PutAsync(
                key,
                JsonSerializer.SerializeToUtf8Bytes(document, SerializerOptions),
                ContentType,
                cancellationToken);
            keys.Add(key);
        }

        return new ArchiveReadingsResult(windowStart, windowEnd, keys);
    }
}
