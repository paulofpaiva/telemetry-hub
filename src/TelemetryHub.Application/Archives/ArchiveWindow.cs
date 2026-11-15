namespace TelemetryHub.Application.Archives;

public static class ArchiveWindow
{
    /// <summary>
    /// The most recent window that is fully in the past. Windows are aligned to the clock
    /// (a 5 minute window always starts at :00, :05, :10...), and a window only counts as
    /// complete once <paramref name="grace"/> has passed after its end, to give late readings
    /// time to land in the store.
    /// </summary>
    public static (DateTimeOffset Start, DateTimeOffset End) LastComplete(DateTimeOffset now, TimeSpan size, TimeSpan grace)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(size, TimeSpan.Zero);

        var reference = (now - grace).ToUniversalTime();
        var end = new DateTimeOffset(reference.Ticks - (reference.Ticks % size.Ticks), TimeSpan.Zero);

        return (end - size, end);
    }
}
