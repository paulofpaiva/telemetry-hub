using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using TelemetryHub.IntegrationTests.Fixtures;

namespace TelemetryHub.IntegrationTests;

[Collection(TelemetryHubCollection.Name)]
public class DeviceApiTests(TelemetryHubFixture fixture)
{
    private readonly HttpClient _client = fixture.Client;

    [Fact]
    public async Task Readiness_reports_every_dependency_as_healthy()
    {
        var response = await _client.GetAsync("/health/ready");
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Healthy", body.GetProperty("status").GetString());
        Assert.Equal(
            ["mqtt", "postgres", "questdb", "s3"],
            body.GetProperty("checks").EnumerateArray().Select(check => check.GetProperty("name").GetString()).Order());
    }

    [Fact]
    public async Task Device_can_be_created_read_updated_listed_and_deleted()
    {
        var created = await _client.PostAsJsonAsync("/api/devices", new { deviceId = "crud-pump", name = "Feed pump", location = "Unit A" });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        Assert.Equal("/api/devices/crud-pump", created.Headers.Location?.AbsolutePath);

        var fetched = await _client.GetFromJsonAsync<JsonElement>("/api/devices/crud-pump");
        Assert.Equal("Feed pump", fetched.GetProperty("name").GetString());
        Assert.True(fetched.GetProperty("isActive").GetBoolean());

        var updated = await _client.PutAsJsonAsync("/api/devices/crud-pump", new { name = "Feed pump B", location = (string?)null, isActive = false });
        Assert.Equal(HttpStatusCode.OK, updated.StatusCode);

        var inactive = await _client.GetFromJsonAsync<JsonElement>("/api/devices?isActive=false");
        Assert.Contains(
            inactive.GetProperty("items").EnumerateArray(),
            item => item.GetProperty("deviceId").GetString() == "crud-pump" && item.GetProperty("name").GetString() == "Feed pump B");

        var deleted = await _client.DeleteAsync("/api/devices/crud-pump");
        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await _client.GetAsync("/api/devices/crud-pump")).StatusCode);
    }

    [Fact]
    public async Task Duplicate_device_id_returns_conflict_and_bad_input_returns_validation_problem()
    {
        var first = await _client.PostAsJsonAsync("/api/devices", new { deviceId = "dup-pump", name = "First" });
        var second = await _client.PostAsJsonAsync("/api/devices", new { deviceId = "dup-pump", name = "Second" });
        var invalid = await _client.PostAsJsonAsync("/api/devices", new { deviceId = "Not Valid", name = "" });

        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);

        var problem = await invalid.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(problem.GetProperty("errors").TryGetProperty("DeviceId", out _));
        Assert.True(problem.GetProperty("errors").TryGetProperty("Name", out _));
    }
}
