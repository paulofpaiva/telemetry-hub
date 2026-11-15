using MediatR;
using TelemetryHub.Application.Abstractions;
using TelemetryHub.Domain.Readings;

namespace TelemetryHub.Application.Readings.IngestReading;

public sealed class IngestReadingCommandHandler(IReadingWriter readingWriter) : IRequestHandler<IngestReadingCommand>
{
    public async Task Handle(IngestReadingCommand request, CancellationToken cancellationToken)
    {
        var reading = new Reading(
            request.DeviceId,
            request.SensorType,
            request.Value,
            request.Unit.Trim(),
            request.Timestamp.ToUniversalTime());

        await readingWriter.WriteAsync(reading, cancellationToken);
    }
}
