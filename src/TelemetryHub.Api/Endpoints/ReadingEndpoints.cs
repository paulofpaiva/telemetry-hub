using MediatR;
using TelemetryHub.Application.Readings.GetLatestReadings;
using TelemetryHub.Application.Readings.GetReadingSeries;

namespace TelemetryHub.Api.Endpoints;

internal static class ReadingEndpoints
{
    public static IEndpointRouteBuilder MapReadingEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/devices/{deviceId}/readings").WithTags("Readings");

        group.MapGet("/latest", async (string deviceId, ISender sender, CancellationToken ct) =>
                TypedResults.Ok(await sender.Send(new GetLatestReadingsQuery(deviceId), ct)))
            .WithName("GetLatestReadings")
            .WithSummary("Most recent reading of each sensor of a device.")
            .ProducesValidationProblem();

        group.MapGet("/series", async (
                string deviceId,
                string sensorType,
                ISender sender,
                CancellationToken ct,
                DateTimeOffset? from = null,
                DateTimeOffset? to = null,
                string? interval = null) =>
                TypedResults.Ok(await sender.Send(new GetReadingSeriesQuery(deviceId, sensorType, from, to, interval), ct)))
            .WithName("GetReadingSeries")
            .WithSummary("Time series of one sensor with avg, min and max per window.")
            .WithDescription(
                "Defaults to the last hour in 1 minute windows. 'interval' is a number followed by s, m, h or d, " +
                "for example 30s or 5m. Windows without readings are omitted.")
            .ProducesValidationProblem();

        return app;
    }
}
