using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace TelemetryHub.Infrastructure.QuestDb;

/// <summary>Creates the readings table before ingestion starts.</summary>
internal sealed partial class QuestDbSchemaInitializer(
    QuestDbDataSource dataSource,
    ILogger<QuestDbSchemaInitializer> logger) : IHostedService
{
    private const int MaxAttempts = 10;
    private static readonly TimeSpan RetryDelay = TimeSpan.FromSeconds(2);

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
                await using var command = connection.CreateCommand();
                command.CommandText = QuestDbSchema.CreateReadingsTable;
                await command.ExecuteNonQueryAsync(cancellationToken);

                LogReady(logger, QuestDbSchema.ReadingsTable);
                return;
            }
            catch (Exception exception) when (exception is not OperationCanceledException && attempt < MaxAttempts)
            {
                LogRetry(logger, exception, attempt, MaxAttempts);
                await Task.Delay(RetryDelay, cancellationToken);
            }
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    [LoggerMessage(Level = LogLevel.Information, Message = "QuestDB table {Table} is ready")]
    private static partial void LogReady(ILogger logger, string table);

    [LoggerMessage(Level = LogLevel.Warning, Message = "QuestDB is not reachable yet (attempt {Attempt}/{MaxAttempts})")]
    private static partial void LogRetry(ILogger logger, Exception exception, int attempt, int maxAttempts);
}
