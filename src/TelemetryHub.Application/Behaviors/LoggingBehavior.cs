using System.Diagnostics;
using MediatR;
using Microsoft.Extensions.Logging;

namespace TelemetryHub.Application.Behaviors;

/// <summary>Logs every request with its elapsed time, and failures with the exception.</summary>
public sealed partial class LoggingBehavior<TRequest, TResponse>(ILogger<LoggingBehavior<TRequest, TResponse>> logger)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        var requestName = typeof(TRequest).Name;
        var startedAt = Stopwatch.GetTimestamp();

        try
        {
            var response = await next(cancellationToken);
            LogHandled(logger, requestName, Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds);
            return response;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            LogFailed(logger, exception, requestName, Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds);
            throw;
        }
    }

    // Debug level on success: ingestion runs this for every reading.
    [LoggerMessage(Level = LogLevel.Debug, Message = "Handled {RequestName} in {ElapsedMs:0.00} ms")]
    private static partial void LogHandled(ILogger logger, string requestName, double elapsedMs);

    [LoggerMessage(Level = LogLevel.Warning, Message = "{RequestName} failed after {ElapsedMs:0.00} ms")]
    private static partial void LogFailed(ILogger logger, Exception exception, string requestName, double elapsedMs);
}
