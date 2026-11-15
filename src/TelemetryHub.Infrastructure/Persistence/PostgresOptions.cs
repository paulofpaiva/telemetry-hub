using System.ComponentModel.DataAnnotations;
using Npgsql;

namespace TelemetryHub.Infrastructure.Persistence;

public sealed class PostgresOptions
{
    public const string SectionName = "Postgres";

    [Required]
    public string Host { get; set; } = string.Empty;

    [Range(1, 65535)]
    public int Port { get; set; } = 5432;

    [Required]
    public string Database { get; set; } = string.Empty;

    [Required]
    public string Username { get; set; } = string.Empty;

    [Required]
    public string Password { get; set; } = string.Empty;

    /// <summary>Applies pending EF Core migrations when the API starts.</summary>
    public bool MigrateOnStartup { get; set; } = true;

    public string BuildConnectionString() => new NpgsqlConnectionStringBuilder
    {
        Host = Host,
        Port = Port,
        Database = Database,
        Username = Username,
        Password = Password,
    }.ConnectionString;
}
