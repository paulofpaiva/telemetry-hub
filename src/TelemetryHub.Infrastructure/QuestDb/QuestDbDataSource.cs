using Microsoft.Extensions.Options;
using Npgsql;

namespace TelemetryHub.Infrastructure.QuestDb;

/// <summary>
/// Connection pool for QuestDB over the PostgreSQL wire protocol. Wrapped in its own type so it
/// cannot be confused with the PostgreSQL data source used by EF Core.
/// </summary>
public sealed class QuestDbDataSource(IOptions<QuestDbOptions> options) : IAsyncDisposable
{
    private readonly NpgsqlDataSource _dataSource = Build(options.Value);

    public ValueTask<NpgsqlConnection> OpenConnectionAsync(CancellationToken cancellationToken) =>
        _dataSource.OpenConnectionAsync(cancellationToken);

    public ValueTask DisposeAsync() => _dataSource.DisposeAsync();

    private static NpgsqlDataSource Build(QuestDbOptions options)
    {
        var builder = new NpgsqlDataSourceBuilder(options.BuildPgConnectionString());

        // QuestDB does not expose the PostgreSQL type catalog that Npgsql loads by default.
        builder.ConfigureTypeLoading(typeLoading => typeLoading.EnableTypeLoading(false));

        return builder.Build();
    }
}
