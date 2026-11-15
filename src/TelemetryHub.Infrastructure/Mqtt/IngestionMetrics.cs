using System.Diagnostics.Metrics;

namespace TelemetryHub.Infrastructure.Mqtt;

/// <summary>Counters for the ingestion pipeline, exposed through System.Diagnostics.Metrics.</summary>
public sealed class IngestionMetrics : IDisposable
{
    public const string MeterName = "TelemetryHub.Ingestion";

    private readonly Meter _meter;
    private readonly Counter<long> _accepted;
    private readonly Counter<long> _rejected;

    public IngestionMetrics(IMeterFactory meterFactory)
    {
        _meter = meterFactory.Create(MeterName);
        _accepted = _meter.CreateCounter<long>("telemetryhub.readings.accepted", description: "Readings accepted for storage.");
        _rejected = _meter.CreateCounter<long>("telemetryhub.readings.rejected", description: "Readings dropped as invalid.");
    }

    public void Accepted() => _accepted.Add(1);

    public void Rejected(string reason) => _rejected.Add(1, new KeyValuePair<string, object?>("reason", reason));

    public void Dispose() => _meter.Dispose();
}
