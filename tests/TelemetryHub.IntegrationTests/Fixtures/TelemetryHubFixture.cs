using System.Text;
using Amazon.Runtime;
using Amazon.S3;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Testcontainers.PostgreSql;

namespace TelemetryHub.IntegrationTests.Fixtures;

/// <summary>
/// Starts the real dependencies in containers (PostgreSQL, QuestDB, Mosquitto and an
/// S3-compatible store) and hosts the API in-process against them. Shared by every test in
/// the collection, so containers start once per test run.
/// </summary>
public sealed class TelemetryHubFixture : IAsyncLifetime
{
    public const string TopicPrefix = "telemetry";

    private const string Bucket = "telemetry-archive-tests";
    private const string S3AccessKey = "integration";
    private const string S3SecretKey = "integration-secret";

    private const string MosquittoConfig = """
        listener 1883
        allow_anonymous true
        persistence false
        """;

    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:17-alpine")
        .WithDatabase("telemetry_hub")
        .WithUsername("telemetry")
        .WithPassword("telemetry")
        .Build();

    private readonly IContainer _questDb = new ContainerBuilder("questdb/questdb:10.2.0")
        .WithPortBinding(9000, assignRandomHostPort: true)
        .WithPortBinding(8812, assignRandomHostPort: true)
        .WithEnvironment("QDB_TELEMETRY_ENABLED", "false")
        .WithWaitStrategy(Wait.ForUnixContainer().UntilHttpRequestIsSucceeded(request => request.ForPort(9000).ForPath("/")))
        .Build();

    private readonly IContainer _mosquitto = new ContainerBuilder("eclipse-mosquitto:2.1.2-alpine")
        .WithPortBinding(1883, assignRandomHostPort: true)
        .WithResourceMapping(Encoding.UTF8.GetBytes(MosquittoConfig), "/mosquitto/config/mosquitto.conf")
        .WithWaitStrategy(Wait.ForUnixContainer().UntilMessageIsLogged("mosquitto version .* running"))
        .Build();

    private readonly IContainer _s3 = new ContainerBuilder("rustfs/rustfs:1.0.5")
        .WithPortBinding(9000, assignRandomHostPort: true)
        .WithEnvironment("RUSTFS_ADDRESS", ":9000")
        .WithEnvironment("RUSTFS_ACCESS_KEY", S3AccessKey)
        .WithEnvironment("RUSTFS_SECRET_KEY", S3SecretKey)
        .WithEnvironment("RUSTFS_VOLUMES", "/data")
        .Build();

    private WebApplicationFactory<Program>? _factory;

    public HttpClient Client { get; private set; } = null!;

    public string MqttHost => _mosquitto.Hostname;

    public int MqttPort => _mosquitto.GetMappedPublicPort(1883);

    public async Task InitializeAsync()
    {
        await Task.WhenAll(
            _postgres.StartAsync(),
            _questDb.StartAsync(),
            _mosquitto.StartAsync(),
            _s3.StartAsync());

        var s3Url = $"http://{_s3.Hostname}:{_s3.GetMappedPublicPort(9000)}";
        await CreateBucketAsync(s3Url);

        var settings = new Dictionary<string, string>
        {
            ["Postgres:Host"] = _postgres.Hostname,
            ["Postgres:Port"] = _postgres.GetMappedPublicPort(5432).ToString(System.Globalization.CultureInfo.InvariantCulture),
            ["Postgres:Database"] = "telemetry_hub",
            ["Postgres:Username"] = "telemetry",
            ["Postgres:Password"] = "telemetry",
            ["QuestDb:Host"] = _questDb.Hostname,
            ["QuestDb:HttpPort"] = _questDb.GetMappedPublicPort(9000).ToString(System.Globalization.CultureInfo.InvariantCulture),
            ["QuestDb:PgPort"] = _questDb.GetMappedPublicPort(8812).ToString(System.Globalization.CultureInfo.InvariantCulture),
            ["QuestDb:Username"] = "admin",
            ["QuestDb:Password"] = "quest",
            ["QuestDb:Database"] = "qdb",
            ["QuestDb:BatchSize"] = "10",
            ["QuestDb:FlushIntervalMs"] = "100",
            ["Mqtt:Host"] = MqttHost,
            ["Mqtt:Port"] = MqttPort.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ["Mqtt:TopicPrefix"] = TopicPrefix,
            ["Mqtt:ClientId"] = "telemetry-hub-integration-tests",
            ["S3:ServiceUrl"] = s3Url,
            ["S3:ForcePathStyle"] = "true",
            ["S3:Region"] = "us-east-1",
            ["S3:Bucket"] = Bucket,
            ["S3:AccessKey"] = S3AccessKey,
            ["S3:SecretKey"] = S3SecretKey,

            // The background job would race with the tests; they archive on demand instead.
            ["Archive:IntervalSeconds"] = "86400",
        };

        _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Development");
            foreach (var (key, value) in settings)
            {
                builder.UseSetting(key, value);
            }
        });

        Client = _factory.CreateClient();
    }

    public async Task DisposeAsync()
    {
        Client?.Dispose();
        if (_factory is not null)
        {
            await _factory.DisposeAsync();
        }

        await Task.WhenAll(
            _postgres.DisposeAsync().AsTask(),
            _questDb.DisposeAsync().AsTask(),
            _mosquitto.DisposeAsync().AsTask(),
            _s3.DisposeAsync().AsTask());
    }

    /// <summary>Retries until the condition holds. Ingestion is asynchronous end to end.</summary>
    public static async Task<T> EventuallyAsync<T>(Func<Task<T>> probe, Func<T, bool> until, int timeoutSeconds = 30)
    {
        var deadline = DateTime.UtcNow.AddSeconds(timeoutSeconds);
        while (true)
        {
            var value = await probe();
            if (until(value))
            {
                return value;
            }

            if (DateTime.UtcNow > deadline)
            {
                throw new TimeoutException($"Condition not met within {timeoutSeconds} seconds.");
            }

            await Task.Delay(250);
        }
    }

    private static async Task CreateBucketAsync(string serviceUrl)
    {
        using var client = new AmazonS3Client(
            new BasicAWSCredentials(S3AccessKey, S3SecretKey),
            new AmazonS3Config
            {
                ServiceURL = serviceUrl,
                ForcePathStyle = true,
                AuthenticationRegion = "us-east-1",
                RequestChecksumCalculation = RequestChecksumCalculation.WHEN_REQUIRED,
                ResponseChecksumValidation = ResponseChecksumValidation.WHEN_REQUIRED,
            });

        // The container reports "started" before the S3 endpoint accepts requests.
        await EventuallyAsync(
            async () =>
            {
                try
                {
                    await client.PutBucketAsync(Bucket);
                    return true;
                }
                catch (Exception exception) when (exception is AmazonServiceException or HttpRequestException)
                {
                    return false;
                }
            },
            created => created);
    }
}

[CollectionDefinition(Name)]
public sealed class TelemetryHubCollection : ICollectionFixture<TelemetryHubFixture>
{
    public const string Name = "telemetry-hub";
}
