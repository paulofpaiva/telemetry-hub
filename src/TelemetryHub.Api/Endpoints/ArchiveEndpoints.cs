using MediatR;
using TelemetryHub.Application.Archives;

namespace TelemetryHub.Api.Endpoints;

internal static class ArchiveEndpoints
{
    public sealed record RunArchiveRequest(DateTimeOffset WindowStart, DateTimeOffset WindowEnd);

    public static IEndpointRouteBuilder MapArchiveEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/archives").WithTags("Archives");

        group.MapGet("/", async (ISender sender, CancellationToken ct, DateOnly? date = null, string? deviceId = null, int limit = 100) =>
                TypedResults.Ok(await sender.Send(new ListArchivesQuery(date, deviceId, limit), ct)))
            .WithName("ListArchives")
            .WithSummary("Lists archived files, optionally filtered by day (yyyy-MM-dd) and device.")
            .ProducesValidationProblem();

        group.MapGet("/files/{**key}", async (string key, ISender sender, CancellationToken ct) =>
            {
                var file = await sender.Send(new GetArchiveFileQuery(key), ct);

                // The result disposes the stream after writing the response.
                return TypedResults.Stream(file.Content, file.ContentType, Path.GetFileName(key));
            })
            .WithName("DownloadArchive")
            .WithSummary("Downloads one archived file by its key.")
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPost("/run", async (RunArchiveRequest request, ISender sender, CancellationToken ct) =>
                TypedResults.Ok(await sender.Send(new ArchiveReadingsCommand(request.WindowStart, request.WindowEnd), ct)))
            .WithName("RunArchive")
            .WithSummary("Archives a specific window on demand, in addition to the background job.")
            .ProducesValidationProblem();

        return app;
    }
}
