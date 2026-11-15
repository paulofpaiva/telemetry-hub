using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TelemetryHub.Application.Archives;

namespace TelemetryHub.Infrastructure.Archiving;

/// <summary>
/// Periodically archives the last complete window. Each window is archived once per process;
/// because archive keys are deterministic, a restart that repeats a window just overwrites
/// the same objects.
/// </summary>
internal sealed partial class ArchiveJob(
    IServiceScopeFactory scopeFactory,
    IOptions<ArchiveOptions> options,
    TimeProvider timeProvider,
    ILogger<ArchiveJob> logger) : BackgroundService
{
    private readonly ArchiveOptions _options = options.Value;
    private DateTimeOffset? _lastArchivedWindowEnd;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(_options.IntervalSeconds), timeProvider);

        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                await RunOnceAsync(stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Normal shutdown.
        }
    }

    private async Task RunOnceAsync(CancellationToken cancellationToken)
    {
        var (start, end) = ArchiveWindow.LastComplete(
            timeProvider.GetUtcNow(),
            TimeSpan.FromMinutes(_options.WindowMinutes),
            TimeSpan.FromSeconds(_options.GraceSeconds));

        if (end == _lastArchivedWindowEnd)
        {
            return;
        }

        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var result = await scope.ServiceProvider.GetRequiredService<ISender>()
                .Send(new ArchiveReadingsCommand(start, end), cancellationToken);

            _lastArchivedWindowEnd = end;
            LogArchived(logger, start, end, result.Keys.Count);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            // Leave the window unmarked so the next tick tries it again.
            LogFailed(logger, exception, start, end);
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Archived window {WindowStart:o} to {WindowEnd:o} into {FileCount} files")]
    private static partial void LogArchived(ILogger logger, DateTimeOffset windowStart, DateTimeOffset windowEnd, int fileCount);

    [LoggerMessage(Level = LogLevel.Error, Message = "Archiving window {WindowStart:o} to {WindowEnd:o} failed, will retry")]
    private static partial void LogFailed(ILogger logger, Exception exception, DateTimeOffset windowStart, DateTimeOffset windowEnd);
}
