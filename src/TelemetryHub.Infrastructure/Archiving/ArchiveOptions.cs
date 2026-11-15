using System.ComponentModel.DataAnnotations;

namespace TelemetryHub.Infrastructure.Archiving;

public sealed class ArchiveOptions
{
    public const string SectionName = "Archive";

    /// <summary>How often the job checks whether a new window is ready to be archived.</summary>
    [Range(1, 86_400)]
    public int IntervalSeconds { get; set; } = 60;

    /// <summary>Size of each archived window. Windows are aligned to the clock.</summary>
    [Range(1, 1440)]
    public int WindowMinutes { get; set; } = 5;

    /// <summary>Wait after a window ends before archiving it, so late readings are included.</summary>
    [Range(0, 3600)]
    public int GraceSeconds { get; set; } = 10;
}
