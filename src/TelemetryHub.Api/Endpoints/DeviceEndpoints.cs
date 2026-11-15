using MediatR;
using TelemetryHub.Application.Devices;
using TelemetryHub.Application.Devices.CreateDevice;
using TelemetryHub.Application.Devices.DeleteDevice;
using TelemetryHub.Application.Devices.GetDevice;
using TelemetryHub.Application.Devices.ListDevices;
using TelemetryHub.Application.Devices.UpdateDevice;

namespace TelemetryHub.Api.Endpoints;

internal static class DeviceEndpoints
{
    public sealed record CreateDeviceRequest(string DeviceId, string Name, string? Location);

    public sealed record UpdateDeviceRequest(string Name, string? Location, bool IsActive);

    public static IEndpointRouteBuilder MapDeviceEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/devices").WithTags("Devices");

        group.MapGet("/", async (ISender sender, CancellationToken ct, int page = 1, int pageSize = 20, bool? isActive = null) =>
                TypedResults.Ok(await sender.Send(new ListDevicesQuery(page, pageSize, isActive), ct)))
            .WithName("ListDevices")
            .WithSummary("Lists registered devices, paged.")
            .ProducesValidationProblem();

        group.MapGet("/{deviceId}", async (string deviceId, ISender sender, CancellationToken ct) =>
                TypedResults.Ok(await sender.Send(new GetDeviceQuery(deviceId), ct)))
            .WithName("GetDevice")
            .WithSummary("Gets one device by its public device id.")
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPost("/", async (CreateDeviceRequest request, ISender sender, CancellationToken ct) =>
            {
                var device = await sender.Send(new CreateDeviceCommand(request.DeviceId, request.Name, request.Location), ct);
                return TypedResults.CreatedAtRoute(device, "GetDevice", new { deviceId = device.DeviceId });
            })
            .WithName("CreateDevice")
            .WithSummary("Registers a new device.")
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapPut("/{deviceId}", async (string deviceId, UpdateDeviceRequest request, ISender sender, CancellationToken ct) =>
                TypedResults.Ok(await sender.Send(
                    new UpdateDeviceCommand(deviceId, request.Name, request.Location, request.IsActive), ct)))
            .WithName("UpdateDevice")
            .WithSummary("Updates a device.")
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapDelete("/{deviceId}", async (string deviceId, ISender sender, CancellationToken ct) =>
            {
                await sender.Send(new DeleteDeviceCommand(deviceId), ct);
                return TypedResults.NoContent();
            })
            .WithName("DeleteDevice")
            .WithSummary("Removes a device from the registry.")
            .ProducesProblem(StatusCodes.Status404NotFound);

        return app;
    }
}
