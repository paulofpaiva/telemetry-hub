using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using TelemetryHub.Application.Abstractions;
using TelemetryHub.Infrastructure.Persistence;
using TelemetryHub.Infrastructure.Readings;

namespace TelemetryHub.Infrastructure;

public static class DependencyInjection
{
    /// <summary>Tag for health checks that must pass before the API is considered ready.</summary>
    public const string ReadyTag = "ready";

    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddPersistence(configuration);
        services.AddSingleton<IReadingWriter, DiscardingReadingWriter>();

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
}
