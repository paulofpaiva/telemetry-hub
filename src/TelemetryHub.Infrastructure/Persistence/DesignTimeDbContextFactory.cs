using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace TelemetryHub.Infrastructure.Persistence;

/// <summary>
/// Used only by the "dotnet ef" tooling to build the model when adding migrations.
/// No connection is opened, so the connection string is a placeholder.
/// </summary>
internal sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql("Host=design-time-only")
            .Options;

        return new AppDbContext(options);
    }
}
