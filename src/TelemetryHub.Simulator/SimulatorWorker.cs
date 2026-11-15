using System.Text.Json;
using Microsoft.Extensions.Options;
using MQTTnet;
using MQTTnet.Protocol;

namespace TelemetryHub.Simulator;

/// <summary>Publishes fake sensor readings to "{prefix}/{deviceId}/{sensorType}" at a fixed rate.</summary>
public sealed partial class SimulatorWorker(
    IOptions<MqttOptions> mqttOptions,
    IOptions<SimulatorOptions> simulatorOptions,
    TimeProvider timeProvider,
    ILogger<SimulatorWorker> logger) : BackgroundService
{
    private static readonly TimeSpan ReconnectDelay = TimeSpan.FromSeconds(3);

    private readonly MqttOptions _mqtt = mqttOptions.Value;
    private readonly SimulatorOptions _simulator = simulatorOptions.Value;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var devices = Enumerable.Range(1, _simulator.DeviceCount)
            .Select(index => (Id: $"{_simulator.DevicePrefix}-{index:000}", Signals: SensorSignal.CreateForDevice(Random.Shared)))
            .ToArray();

        using var client = new MqttClientFactory().CreateMqttClient();
        var clientOptions = BuildClientOptions();
        using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(_simulator.IntervalMs), timeProvider);

        long published = 0;

        try
        {
            do
            {
                if (!client.IsConnected && !await TryConnectAsync(client, clientOptions, stoppingToken))
                {
                    await Task.Delay(ReconnectDelay, timeProvider, stoppingToken);
                    continue;
                }

                try
                {
                    var now = timeProvider.GetUtcNow();
                    foreach (var (deviceId, signals) in devices)
                    {
                        foreach (var signal in signals)
                        {
                            await client.PublishAsync(BuildMessage(deviceId, signal, now), stoppingToken);
                            published++;
                        }
                    }

                    LogRound(logger, devices.Length, published);
                }
                catch (Exception exception) when (exception is not OperationCanceledException)
                {
                    // Most likely the broker went away; the next iteration reconnects.
                    LogPublishFailed(logger, exception);
                }
            }
            while (await timer.WaitForNextTickAsync(stoppingToken));
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Normal shutdown.
        }

        if (client.IsConnected)
        {
            await client.DisconnectAsync(cancellationToken: CancellationToken.None);
        }
    }

    private MqttClientOptions BuildClientOptions()
    {
        var builder = new MqttClientOptionsBuilder()
            .WithTcpServer(_mqtt.Host, _mqtt.Port)
            .WithClientId($"telemetry-hub-simulator-{Guid.NewGuid():N}")
            .WithCleanSession();

        if (!string.IsNullOrEmpty(_mqtt.Username))
        {
            builder.WithCredentials(_mqtt.Username, _mqtt.Password);
        }

        return builder.Build();
    }

    private async Task<bool> TryConnectAsync(IMqttClient client, MqttClientOptions options, CancellationToken cancellationToken)
    {
        try
        {
            await client.ConnectAsync(options, cancellationToken);
            LogConnected(logger, _mqtt.Host, _mqtt.Port, _simulator.DeviceCount, _simulator.IntervalMs);
            return true;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            LogConnectFailed(logger, exception, _mqtt.Host, _mqtt.Port);
            return false;
        }
    }

    private MqttApplicationMessage BuildMessage(string deviceId, SensorSignal signal, DateTimeOffset now)
    {
        var payload = JsonSerializer.SerializeToUtf8Bytes(
            new ReadingPayload(signal.Next(now), signal.Unit, now),
            JsonSerializerOptions.Web);

        return new MqttApplicationMessageBuilder()
            .WithTopic($"{_mqtt.TopicPrefix}/{deviceId}/{signal.SensorType}")
            .WithPayload(payload)
            .WithContentType("application/json")
            .WithQualityOfServiceLevel(MqttQualityOfServiceLevel.AtLeastOnce)
            .Build();
    }

    private sealed record ReadingPayload(double Value, string Unit, DateTimeOffset Timestamp);

    [LoggerMessage(Level = LogLevel.Information,
        Message = "Connected to MQTT broker {Host}:{Port}, simulating {DeviceCount} devices every {IntervalMs} ms")]
    private static partial void LogConnected(ILogger logger, string host, int port, int deviceCount, int intervalMs);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Could not connect to MQTT broker {Host}:{Port}, retrying")]
    private static partial void LogConnectFailed(ILogger logger, Exception exception, string host, int port);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Publishing failed, will reconnect")]
    private static partial void LogPublishFailed(ILogger logger, Exception exception);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Published a round for {DeviceCount} devices ({Total} readings so far)")]
    private static partial void LogRound(ILogger logger, int deviceCount, long total);
}
