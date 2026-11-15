using Microsoft.Extensions.DependencyInjection;
using TelemetryHub.Application.Abstractions;
using TelemetryHub.Infrastructure.Readings;

namespace TelemetryHub.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services)
    {
        services.AddSingleton<IReadingWriter, DiscardingReadingWriter>();

        return services;
    }
}
