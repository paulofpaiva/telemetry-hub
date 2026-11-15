using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using TelemetryHub.Application.Abstractions;

namespace TelemetryHub.Infrastructure.QuestDb;

/// <summary>
/// Flushes the reading writer on a timer so a slow trickle of readings does not sit in the
/// buffer waiting for a full batch.
/// </summary>
internal sealed class QuestDbFlushService(
    IReadingWriter readingWriter,
    IOptions<QuestDbOptions> options,
    TimeProvider timeProvider) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(options.Value.FlushIntervalMs), timeProvider);

        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                await readingWriter.FlushAsync(stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Normal shutdown; the writer flushes what is left when it is disposed.
        }
    }
}
