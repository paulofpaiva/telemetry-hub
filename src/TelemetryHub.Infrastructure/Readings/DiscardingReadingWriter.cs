using TelemetryHub.Application.Abstractions;
using TelemetryHub.Domain.Readings;

namespace TelemetryHub.Infrastructure.Readings;

/// <summary>
/// Temporary writer that drops readings. It only exists so the dependency graph is complete
/// and the API can start; it is replaced by the QuestDB writer.
/// </summary>
internal sealed class DiscardingReadingWriter : IReadingWriter
{
    public ValueTask WriteAsync(Reading reading, CancellationToken cancellationToken) => ValueTask.CompletedTask;

    public ValueTask FlushAsync(CancellationToken cancellationToken) => ValueTask.CompletedTask;
}
