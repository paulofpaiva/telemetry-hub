namespace TelemetryHub.Simulator;

/// <summary>
/// Generates a plausible sensor signal: a baseline with a slow sine drift, random noise
/// and an occasional spike, so charts and aggregations have something to show.
/// </summary>
public sealed class SensorSignal(
    string sensorType,
    string unit,
    double baseline,
    double driftAmplitude,
    double noise,
    double spikeProbability,
    double spikeSize,
    Random random)
{
    private readonly double _phase = random.NextDouble() * Math.Tau;
    private readonly double _periodSeconds = 60 + (random.NextDouble() * 240);

    public string SensorType { get; } = sensorType;

    public string Unit { get; } = unit;

    public double Next(DateTimeOffset now)
    {
        var seconds = now.ToUnixTimeMilliseconds() / 1000.0;
        var drift = driftAmplitude * Math.Sin((seconds / _periodSeconds * Math.Tau) + _phase);
        var jitter = ((random.NextDouble() * 2) - 1) * noise;
        var spike = random.NextDouble() < spikeProbability ? spikeSize * (0.5 + random.NextDouble()) : 0;

        return Math.Round(Math.Max(0, baseline + drift + jitter + spike), 3);
    }

    /// <summary>The three sensors every simulated device carries.</summary>
    public static SensorSignal[] CreateForDevice(Random random) =>
    [
        new("pressure", "bar", baseline: 8.0 + random.NextDouble(), driftAmplitude: 0.6, noise: 0.08,
            spikeProbability: 0.005, spikeSize: 2.0, random),
        new("temperature", "degC", baseline: 62.0 + (random.NextDouble() * 6), driftAmplitude: 4.0, noise: 0.3,
            spikeProbability: 0.002, spikeSize: 12.0, random),
        new("vibration", "mm/s", baseline: 2.2 + (random.NextDouble() * 0.6), driftAmplitude: 0.4, noise: 0.15,
            spikeProbability: 0.01, spikeSize: 4.0, random),
    ];
}
