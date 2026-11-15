using System.ComponentModel.DataAnnotations;
using Npgsql;

namespace TelemetryHub.Infrastructure.QuestDb;

public sealed class QuestDbOptions
{
    public const string SectionName = "QuestDb";

    [Required]
    public string Host { get; set; } = string.Empty;

    /// <summary>HTTP port, used for ILP writes.</summary>
    [Range(1, 65535)]
    public int HttpPort { get; set; } = 9000;

    /// <summary>PostgreSQL wire protocol port, used for queries.</summary>
    [Range(1, 65535)]
    public int PgPort { get; set; } = 8812;

    [Required]
    public string Username { get; set; } = string.Empty;

    [Required]
    public string Password { get; set; } = string.Empty;

    [Required]
    public string Database { get; set; } = "qdb";

    /// <summary>Rows buffered before a batch is sent.</summary>
    [Range(1, 1_000_000)]
    public int BatchSize { get; set; } = 500;

    /// <summary>Maximum time a reading waits in the buffer before being sent.</summary>
    [Range(50, 60_000)]
    public int FlushIntervalMs { get; set; } = 1000;

    // Auto-flush is off: the writer decides when to send, by row count or by timer.
    public string BuildIlpConnectionString() => $"http::addr={Host}:{HttpPort};auto_flush=off;";

    public string BuildPgConnectionString() => new NpgsqlConnectionStringBuilder
    {
        Host = Host,
        Port = PgPort,
        Database = Database,
        Username = Username,
        Password = Password,
    }.ConnectionString;
}
