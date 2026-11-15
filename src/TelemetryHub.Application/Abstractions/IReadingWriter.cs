using TelemetryHub.Domain.Readings;

namespace TelemetryHub.Application.Abstractions;

/// <summary>
/// Writes readings to the time-series store. Implementations may buffer and flush in batches,
/// so a completed call means "accepted", not necessarily "durable".
/// </summary>
public interface IReadingWriter
{
    ValueTask WriteAsync(Reading reading, CancellationToken cancellationToken);

    /// <summary>Forces any buffered readings to be sent.</summary>
    ValueTask FlushAsync(CancellationToken cancellationToken);
}
