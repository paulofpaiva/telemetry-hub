using System.Net;
using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using TelemetryHub.Application.Abstractions;

namespace TelemetryHub.Infrastructure.Storage;

internal sealed class S3ArchiveStorage(IAmazonS3 s3, IOptions<S3Options> options) : IArchiveStorage, IHealthCheck
{
    private readonly string _bucket = options.Value.Bucket;

    public async Task PutAsync(string key, ReadOnlyMemory<byte> content, string contentType, CancellationToken cancellationToken)
    {
        using var stream = new MemoryStream(content.ToArray(), writable: false);

        await s3.PutObjectAsync(
            new PutObjectRequest
            {
                BucketName = _bucket,
                Key = key,
                InputStream = stream,
                ContentType = contentType,
            },
            cancellationToken);
    }

    public async Task<IReadOnlyList<ArchiveObject>> ListAsync(string prefix, int maxResults, CancellationToken cancellationToken)
    {
        var objects = new List<ArchiveObject>();
        string? continuationToken = null;

        do
        {
            var response = await s3.ListObjectsV2Async(
                new ListObjectsV2Request
                {
                    BucketName = _bucket,
                    Prefix = prefix,
                    MaxKeys = Math.Min(1000, maxResults - objects.Count),
                    ContinuationToken = continuationToken,
                },
                cancellationToken);

            // S3Objects is null, not empty, when nothing matches.
            foreach (var item in response.S3Objects ?? [])
            {
                objects.Add(new ArchiveObject(
                    item.Key,
                    item.Size ?? 0,
                    new DateTimeOffset((item.LastModified ?? DateTime.UnixEpoch).ToUniversalTime())));
            }

            continuationToken = response.IsTruncated == true ? response.NextContinuationToken : null;
        }
        while (continuationToken is not null && objects.Count < maxResults);

        return objects;
    }

    public async Task<ArchiveContent?> OpenReadAsync(string key, CancellationToken cancellationToken)
    {
        try
        {
            var response = await s3.GetObjectAsync(_bucket, key, cancellationToken);

            return new ArchiveContent(
                response.ResponseStream,
                response.Headers.ContentType ?? "application/octet-stream",
                response.ContentLength);
        }
        catch (AmazonS3Exception exception) when (exception.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            await s3.ListObjectsV2Async(new ListObjectsV2Request { BucketName = _bucket, MaxKeys = 1 }, cancellationToken);
            return HealthCheckResult.Healthy();
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return HealthCheckResult.Unhealthy($"Bucket '{_bucket}' is not reachable.", exception);
        }
    }
}
