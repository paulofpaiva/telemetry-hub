using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using QuestDB;
using QuestDB.Senders;
using TelemetryHub.Application.Abstractions;
using TelemetryHub.Domain.Readings;

namespace TelemetryHub.Infrastructure.QuestDb;

/// <summary>
/// Writes readings to QuestDB over ILP/HTTP in batches. Rows accumulate in the client buffer and
/// are sent when <see cref="QuestDbOptions.BatchSize"/> is reached or when
/// <see cref="QuestDbFlushService"/> calls <see cref="FlushAsync"/> on its timer.
/// </summary>
internal sealed partial class QuestDbReadingWriter : IReadingWriter, IAsyncDisposable
{
    private readonly QuestDbOptions _options;
    private readonly ILogger<QuestDbReadingWriter> _logger;

    // The ILP sender is not thread-safe; every access goes through this lock.
    private readonly SemaphoreSlim _lock = new(1, 1);
    private ISender _sender;

    public QuestDbReadingWriter(IOptions<QuestDbOptions> options, ILogger<QuestDbReadingWriter> logger)
    {
        _options = options.Value;
        _logger = logger;
        _sender = Sender.New(_options.BuildIlpConnectionString());
    }

    public async ValueTask WriteAsync(Reading reading, CancellationToken cancellationToken)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            await _sender.Table(QuestDbSchema.ReadingsTable)
                .Symbol("device_id", reading.DeviceId)
                .Symbol("sensor_type", reading.SensorType.ToSlug())
                .Symbol("unit", reading.Unit)
                .Column("value", reading.Value)
                .AtAsync(reading.Timestamp, cancellationToken);

            if (_sender.RowCount >= _options.BatchSize)
            {
                await SendBufferedAsync(cancellationToken);
            }
        }
        finally
        {
            _lock.Release();
        }
    }

    public async ValueTask FlushAsync(CancellationToken cancellationToken)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            await SendBufferedAsync(cancellationToken);
        }
        finally
        {
            _lock.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        await _lock.WaitAsync();
        try
        {
            // Best effort: do not lose what is still buffered on shutdown.
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            await SendBufferedAsync(timeout.Token);
        }
        catch (OperationCanceledException)
        {
            // Shutdown must not hang on an unreachable database.
        }
        finally
        {
            _sender.Dispose();
            _lock.Release();
            _lock.Dispose();
        }
    }

    private async Task SendBufferedAsync(CancellationToken cancellationToken)
    {
        var rows = _sender.RowCount;
        if (rows == 0)
        {
            return;
        }

        try
        {
            await _sender.SendAsync(cancellationToken);
            LogBatchSent(_logger, rows);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            // The client already retried transient HTTP failures. Drop the batch and start from a
            // clean sender so one bad batch cannot block every reading that comes after it.
            LogBatchDropped(_logger, exception, rows);
            _sender.Dispose();
            _sender = Sender.New(_options.BuildIlpConnectionString());
        }
    }

    [LoggerMessage(Level = LogLevel.Debug, Message = "Sent {Rows} readings to QuestDB")]
    private static partial void LogBatchSent(ILogger logger, int rows);

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to send {Rows} readings to QuestDB, batch dropped")]
    private static partial void LogBatchDropped(ILogger logger, Exception exception, int rows);
}
