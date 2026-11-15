using FluentValidation;
using MediatR;
using TelemetryHub.Application.Abstractions;
using TelemetryHub.Domain.Devices;

namespace TelemetryHub.Application.Archives;

/// <summary>Lists archived files, optionally filtered by day and device.</summary>
public sealed record ListArchivesQuery(DateOnly? Date = null, string? DeviceId = null, int Limit = 100)
    : IRequest<IReadOnlyList<ArchiveFileDto>>;

public sealed record ArchiveFileDto(string Key, long SizeBytes, DateTimeOffset LastModified);

public sealed class ListArchivesQueryValidator : AbstractValidator<ListArchivesQuery>
{
    public const int MaxLimit = 1000;

    public ListArchivesQueryValidator()
    {
        RuleFor(query => query.Limit).InclusiveBetween(1, MaxLimit);

        RuleFor(query => query.DeviceId)
            .Must(value => value is null || DeviceId.IsValid(value))
            .WithMessage("Device id is not valid.");
    }
}

public sealed class ListArchivesQueryHandler(IArchiveStorage storage)
    : IRequestHandler<ListArchivesQuery, IReadOnlyList<ArchiveFileDto>>
{
    public async Task<IReadOnlyList<ArchiveFileDto>> Handle(ListArchivesQuery request, CancellationToken cancellationToken)
    {
        var prefix = ArchiveKey.PrefixFor(request.Date, request.DeviceId);

        // Keys are partitioned by date first. A device filter without a date cannot be expressed
        // as a prefix, so it is applied on the listed keys.
        var filterInMemory = request.Date is null && request.DeviceId is not null;
        var objects = await storage.ListAsync(
            prefix,
            filterInMemory ? ListArchivesQueryValidator.MaxLimit : request.Limit,
            cancellationToken);

        return
        [
            .. objects
                .Where(item => !filterInMemory || ArchiveKey.BelongsToDevice(item.Key, request.DeviceId!))
                .Take(request.Limit)
                .Select(item => new ArchiveFileDto(item.Key, item.SizeBytes, item.LastModified)),
        ];
    }
}
