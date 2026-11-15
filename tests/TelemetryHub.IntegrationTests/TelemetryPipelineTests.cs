using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using MQTTnet;
using MQTTnet.Protocol;
using TelemetryHub.IntegrationTests.Fixtures;

namespace TelemetryHub.IntegrationTests;

/// <summary>End to end: MQTT message in, QuestDB row out, S3 archive file out.</summary>
[Collection(TelemetryHubCollection.Name)]
public class TelemetryPipelineTests(TelemetryHubFixture fixture)
{
    private readonly HttpClient _client = fixture.Client;

    [Fact]
    public async Task Readings_published_over_mqtt_show_up_in_latest_and_series_queries()
    {
        const string deviceId = "it-pump-series";
        var minute = new DateTimeOffset(2026, 1, 31, 10, 15, 0, TimeSpan.Zero);

        await PublishAsync(
            (deviceId, "pressure", """{"value":8.0,"unit":"bar","timestamp":"2026-01-31T10:15:10Z"}"""),
            (deviceId, "pressure", """{"value":9.0,"unit":"bar","timestamp":"2026-01-31T10:15:20Z"}"""),
            (deviceId, "pressure", """{"value":10.0,"unit":"bar","timestamp":"2026-01-31T10:16:05Z"}"""),
            (deviceId, "temperature", """{"value":64.5,"unit":"degC","timestamp":"2026-01-31T10:16:05Z"}"""),

            // Same reading delivered twice, as QoS 1 allows: must be stored once.
            (deviceId, "temperature", """{"value":64.5,"unit":"degC","timestamp":"2026-01-31T10:16:05Z"}"""),

            // Malformed messages must be dropped without affecting the rest.
            (deviceId, "humidity", """{"value":1}"""),
            (deviceId, "pressure", "not json"));

        var latest = await TelemetryHubFixture.EventuallyAsync(
            () => _client.GetFromJsonAsync<JsonElement>($"/api/devices/{deviceId}/readings/latest"),
            body => body.GetArrayLength() == 2 && body[0].GetProperty("value").GetDouble() == 10.0);

        Assert.Equal("pressure", latest[0].GetProperty("sensorType").GetString());
        Assert.Equal("temperature", latest[1].GetProperty("sensorType").GetString());
        Assert.Equal(64.5, latest[1].GetProperty("value").GetDouble());

        var series = await _client.GetFromJsonAsync<JsonElement>(
            $"/api/devices/{deviceId}/readings/series?sensorType=pressure&from=2026-01-31T10:00:00Z&to=2026-01-31T11:00:00Z&interval=1m");
        var points = series.GetProperty("points");

        Assert.Equal(2, points.GetArrayLength());
        Assert.Equal(minute, points[0].GetProperty("timestamp").GetDateTimeOffset());
        Assert.Equal(8.5, points[0].GetProperty("avg").GetDouble());
        Assert.Equal(8.0, points[0].GetProperty("min").GetDouble());
        Assert.Equal(9.0, points[0].GetProperty("max").GetDouble());
        Assert.Equal(2, points[0].GetProperty("count").GetInt64());
        Assert.Equal(1, points[1].GetProperty("count").GetInt64());

        var temperature = await _client.GetFromJsonAsync<JsonElement>(
            $"/api/devices/{deviceId}/readings/series?sensorType=temperature&from=2026-01-31T10:00:00Z&to=2026-01-31T11:00:00Z&interval=1h");
        Assert.Equal(1, temperature.GetProperty("points")[0].GetProperty("count").GetInt64());
    }

    [Fact]
    public async Task Archiving_a_window_stores_a_file_per_device_that_can_be_listed_and_downloaded()
    {
        const string deviceId = "it-pump-archive";

        await PublishAsync(
            (deviceId, "vibration", """{"value":2.0,"unit":"mm/s","timestamp":"2026-02-01T08:00:30Z"}"""),
            (deviceId, "vibration", """{"value":4.0,"unit":"mm/s","timestamp":"2026-02-01T08:02:30Z"}"""),

            // Outside the archived window.
            (deviceId, "vibration", """{"value":99.0,"unit":"mm/s","timestamp":"2026-02-01T08:07:00Z"}"""));

        await TelemetryHubFixture.EventuallyAsync(
            () => _client.GetFromJsonAsync<JsonElement>($"/api/devices/{deviceId}/readings/latest"),
            body => body.GetArrayLength() == 1 && body[0].GetProperty("value").GetDouble() == 99.0);

        var run = await _client.PostAsJsonAsync(
            "/api/archives/run",
            new { windowStart = "2026-02-01T08:00:00Z", windowEnd = "2026-02-01T08:05:00Z" });
        Assert.Equal(HttpStatusCode.OK, run.StatusCode);

        var expectedKey = $"readings/date=2026-02-01/device={deviceId}/20260201T080000Z_20260201T080500Z.json";
        var keys = (await run.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("keys").EnumerateArray().Select(key => key.GetString());
        Assert.Contains(expectedKey, keys);

        var listed = await _client.GetFromJsonAsync<JsonElement>($"/api/archives?date=2026-02-01&deviceId={deviceId}");
        var file = Assert.Single(listed.EnumerateArray());
        Assert.Equal(expectedKey, file.GetProperty("key").GetString());
        Assert.True(file.GetProperty("sizeBytes").GetInt64() > 0);

        var download = await _client.GetAsync($"/api/archives/files/{expectedKey}");
        Assert.Equal(HttpStatusCode.OK, download.StatusCode);
        Assert.Equal("application/json", download.Content.Headers.ContentType?.MediaType);

        var document = await download.Content.ReadFromJsonAsync<JsonElement>();
        var sensor = Assert.Single(document.GetProperty("sensors").EnumerateArray());
        Assert.Equal(deviceId, document.GetProperty("deviceId").GetString());
        Assert.Equal("vibration", sensor.GetProperty("sensorType").GetString());
        Assert.Equal(3.0, sensor.GetProperty("avg").GetDouble());
        Assert.Equal(4.0, sensor.GetProperty("max").GetDouble());
        Assert.Equal(2, sensor.GetProperty("count").GetInt64());
    }

    [Fact]
    public async Task Download_rejects_keys_outside_the_archive_layout_and_unknown_files()
    {
        var outside = await _client.GetAsync("/api/archives/files/other/secret.txt");
        var missing = await _client.GetAsync("/api/archives/files/readings/date=2020-01-01/device=ghost/20200101T000000Z_20200101T000500Z.json");

        Assert.Equal(HttpStatusCode.BadRequest, outside.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
    }

    private async Task PublishAsync(params (string DeviceId, string SensorType, string Payload)[] messages)
    {
        // The subscriber connects in the background after the host starts.
        await TelemetryHubFixture.EventuallyAsync(
            () => _client.GetAsync("/health/ready"),
            response => response.StatusCode == HttpStatusCode.OK);

        using var client = new MqttClientFactory().CreateMqttClient();
        await client.ConnectAsync(new MqttClientOptionsBuilder()
            .WithTcpServer(fixture.MqttHost, fixture.MqttPort)
            .WithClientId(string.Create(CultureInfo.InvariantCulture, $"it-publisher-{Guid.NewGuid():N}"))
            .Build());

        foreach (var (deviceId, sensorType, payload) in messages)
        {
            await client.PublishAsync(new MqttApplicationMessageBuilder()
                .WithTopic($"{TelemetryHubFixture.TopicPrefix}/{deviceId}/{sensorType}")
                .WithPayload(payload)
                .WithQualityOfServiceLevel(MqttQualityOfServiceLevel.AtLeastOnce)
                .Build());
        }

        await client.DisconnectAsync();
    }
}
