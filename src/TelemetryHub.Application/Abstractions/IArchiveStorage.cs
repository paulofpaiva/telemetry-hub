namespace TelemetryHub.Application.Abstractions;

/// <summary>Object storage for archived telemetry files (S3 or any S3-compatible service).</summary>
public interface IArchiveStorage
{
    Task PutAsync(string key, ReadOnlyMemory<byte> content, string contentType, CancellationToken cancellationToken);

    /// <summary>Lists objects under a key prefix, in key order, up to <paramref name="maxResults"/>.</summary>
    Task<IReadOnlyList<ArchiveObject>> ListAsync(string prefix, int maxResults, CancellationToken cancellationToken);

    /// <summary>Opens an object for reading, or returns null when the key does not exist.</summary>
    Task<ArchiveContent?> OpenReadAsync(string key, CancellationToken cancellationToken);
}

public sealed record ArchiveObject(string Key, long SizeBytes, DateTimeOffset LastModified);

/// <summary>An open object. The caller owns the stream and must dispose it.</summary>
public sealed record ArchiveContent(Stream Content, string ContentType, long SizeBytes);
