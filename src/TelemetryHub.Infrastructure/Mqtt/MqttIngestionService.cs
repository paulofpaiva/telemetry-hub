using FluentValidation;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MQTTnet;
using MQTTnet.Protocol;

namespace TelemetryHub.Infrastructure.Mqtt;

/// <summary>
/// Subscribes to the telemetry topics and dispatches one command per message. Also reports the
/// connection state as a health check.
/// </summary>
internal sealed partial class MqttIngestionService(
    IServiceScopeFactory scopeFactory,
    IOptions<MqttOptions> options,
    IngestionMetrics metrics,
    TimeProvider timeProvider,
    ILogger<MqttIngestionService> logger) : BackgroundService, IHealthCheck
{
    private static readonly TimeSpan ReconnectDelay = TimeSpan.FromSeconds(3);

    private readonly MqttOptions _options = options.Value;
    private volatile bool _connected;

    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default) =>
        Task.FromResult(_connected
            ? HealthCheckResult.Healthy()
            : HealthCheckResult.Unhealthy("Not connected to the MQTT broker."));

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var client = new MqttClientFactory().CreateMqttClient();
        var clientOptions = BuildClientOptions();

        client.ApplicationMessageReceivedAsync += args => HandleMessageAsync(args, stoppingToken);
        client.DisconnectedAsync += _ =>
        {
            _connected = false;
            return Task.CompletedTask;
        };

        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                if (!client.IsConnected)
                {
                    await TryConnectAsync(client, clientOptions, stoppingToken);
                }

                await Task.Delay(ReconnectDelay, timeProvider, stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Normal shutdown.
        }

        _connected = false;
        if (client.IsConnected)
        {
            await client.DisconnectAsync(cancellationToken: CancellationToken.None);
        }
    }

    private MqttClientOptions BuildClientOptions()
    {
        var builder = new MqttClientOptionsBuilder()
            .WithTcpServer(_options.Host, _options.Port)
            .WithClientId(_options.ClientId)
            .WithCleanSession();

        if (!string.IsNullOrEmpty(_options.Username))
        {
            builder.WithCredentials(_options.Username, _options.Password);
        }

        return builder.Build();
    }

    private async Task TryConnectAsync(IMqttClient client, MqttClientOptions clientOptions, CancellationToken cancellationToken)
    {
        try
        {
            await client.ConnectAsync(clientOptions, cancellationToken);

            var subscription = new MqttClientSubscribeOptionsBuilder()
                .WithTopicFilter(filter => filter
                    .WithTopic(_options.TopicFilter)
                    .WithQualityOfServiceLevel(MqttQualityOfServiceLevel.AtLeastOnce))
                .Build();
            await client.SubscribeAsync(subscription, cancellationToken);

            _connected = true;
            LogSubscribed(logger, _options.Host, _options.Port, _options.TopicFilter);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            _connected = false;
            LogConnectFailed(logger, exception, _options.Host, _options.Port);
        }
    }

    private async Task HandleMessageAsync(MqttApplicationMessageReceivedEventArgs args, CancellationToken stoppingToken)
    {
        var message = args.ApplicationMessage;

        if (!TelemetryMessageParser.TryParse(
                _options.TopicPrefix,
                message.Topic,
                message.Payload,
                timeProvider.GetUtcNow(),
                out var command,
                out var error))
        {
            metrics.Rejected("malformed");
            LogRejected(logger, message.Topic, error);
            return;
        }

        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            await scope.ServiceProvider.GetRequiredService<ISender>().Send(command, stoppingToken);
            metrics.Accepted();
        }
        catch (ValidationException exception)
        {
            metrics.Rejected("invalid");
            LogRejected(logger, message.Topic, exception.Message);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            // A single bad message must never take the subscription down.
            metrics.Rejected("error");
            LogFailed(logger, exception, message.Topic);
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Connected to MQTT broker {Host}:{Port}, subscribed to {TopicFilter}")]
    private static partial void LogSubscribed(ILogger logger, string host, int port, string topicFilter);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Could not connect to MQTT broker {Host}:{Port}, retrying")]
    private static partial void LogConnectFailed(ILogger logger, Exception exception, string host, int port);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Rejected message on {Topic}: {Reason}")]
    private static partial void LogRejected(ILogger logger, string topic, string reason);

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to ingest message on {Topic}")]
    private static partial void LogFailed(ILogger logger, Exception exception, string topic);
}
