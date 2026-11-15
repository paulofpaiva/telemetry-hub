using FluentValidation;
using MediatR;
using TelemetryHub.Application.Abstractions;

namespace TelemetryHub.Application.Devices.ListDevices;

public sealed record ListDevicesQuery(int Page = 1, int PageSize = 20, bool? IsActive = null)
    : IRequest<PagedResult<DeviceDto>>;

public sealed class ListDevicesQueryValidator : AbstractValidator<ListDevicesQuery>
{
    public const int MaxPageSize = 100;

    public ListDevicesQueryValidator()
    {
        RuleFor(query => query.Page).GreaterThanOrEqualTo(1);
        RuleFor(query => query.PageSize).InclusiveBetween(1, MaxPageSize);
    }
}

public sealed class ListDevicesQueryHandler(IDeviceRepository devices)
    : IRequestHandler<ListDevicesQuery, PagedResult<DeviceDto>>
{
    public async Task<PagedResult<DeviceDto>> Handle(ListDevicesQuery request, CancellationToken cancellationToken)
    {
        var (items, totalCount) = await devices.ListAsync(
            request.Page,
            request.PageSize,
            request.IsActive,
            cancellationToken);

        return new PagedResult<DeviceDto>(
            [.. items.Select(DeviceDto.From)],
            request.Page,
            request.PageSize,
            totalCount);
    }
}
