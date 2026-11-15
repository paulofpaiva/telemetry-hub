using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using TelemetryHub.Application.Abstractions;
using TelemetryHub.Infrastructure.Archiving;
using TelemetryHub.Infrastructure.Mqtt;
using TelemetryHub.Infrastructure.Persistence;
using TelemetryHub.Infrastructure.QuestDb;
using TelemetryHub.Infrastructure.Storage;

namespace TelemetryHub.Infrastructure;

public static class DependencyInjection
{
    /// <summary>Tag for health checks that must pass before the API is considered ready.</summary>
    public const string ReadyTag = "ready";

    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddPersistence(configuration);
        services.AddQuestDb(configuration);
        services.AddMqttIngestion(configuration);
        services.AddArchiving(configuration);

        return services;
    }

    private static void AddPersistence(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<PostgresOptions>()
            .Bind(configuration.GetSection(PostgresOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddDbContext<AppDbContext>((serviceProvider, options) =>
        {
            var postgres = serviceProvider.GetRequiredService<IOptions<PostgresOptions>>().Value;
            options.UseNpgsql(postgres.BuildConnectionString());
        });

        services.AddScoped<IDeviceRepository, DeviceRepository>();
        services.AddHostedService<DatabaseMigrator>();

        services.AddHealthChecks().AddDbContextCheck<AppDbContext>("postgres", tags: [ReadyTag]);
    }

    private static void AddQuestDb(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<QuestDbOptions>()
            .Bind(configuration.GetSection(QuestDbOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddSingleton<QuestDbDataSource>();
        services.AddSingleton<IReadingWriter, QuestDbReadingWriter>();
        services.AddSingleton<IReadingQueries, QuestDbReadingQueries>();

        // Order matters: the table must exist before the flush timer and the subscriber start.
        services.AddHostedService<QuestDbSchemaInitializer>();
        services.AddHostedService<QuestDbFlushService>();

        services.AddHealthChecks().AddCheck<QuestDbHealthCheck>("questdb", tags: [ReadyTag]);
    }

    private static void AddMqttIngestion(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<MqttOptions>()
            .Bind(configuration.GetSection(MqttOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddSingleton<IngestionMetrics>();

        // One instance serves as both the background subscriber and its own health check.
        services.AddSingleton<MqttIngestionService>();
        services.AddHostedService(serviceProvider => serviceProvider.GetRequiredService<MqttIngestionService>());
        services.AddHealthChecks().AddCheck<MqttIngestionService>("mqtt", tags: [ReadyTag]);
    }

    private static void AddArchiving(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<S3Options>()
            .Bind(configuration.GetSection(S3Options.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddOptions<ArchiveOptions>()
            .Bind(configuration.GetSection(ArchiveOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddSingleton(serviceProvider =>
            S3ClientFactory.Create(serviceProvider.GetRequiredService<IOptions<S3Options>>().Value));

        // One instance serves as both the storage and its own health check.
        services.AddSingleton<S3ArchiveStorage>();
        services.AddSingleton<IArchiveStorage>(serviceProvider => serviceProvider.GetRequiredService<S3ArchiveStorage>());
        services.AddHostedService<ArchiveJob>();

        services.AddHealthChecks().AddCheck<S3ArchiveStorage>("s3", tags: [ReadyTag]);
    }
}
