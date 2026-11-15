using System.ComponentModel.DataAnnotations;

namespace TelemetryHub.Infrastructure.Storage;

public sealed class S3Options
{
    public const string SectionName = "S3";

    /// <summary>
    /// Endpoint of an S3-compatible service, for example "http://rustfs:9000". Leave empty to
    /// talk to AWS S3 in <see cref="Region"/>.
    /// </summary>
    public string? ServiceUrl { get; set; }

    /// <summary>Use "endpoint/bucket/key" URLs. Required by most S3-compatible services; AWS uses false.</summary>
    public bool ForcePathStyle { get; set; }

    [Required]
    public string Region { get; set; } = "us-east-1";

    [Required]
    public string Bucket { get; set; } = string.Empty;

    /// <summary>Leave both keys empty to use the default AWS credential chain (env, profile, IAM role).</summary>
    public string? AccessKey { get; set; }

    public string? SecretKey { get; set; }
}
