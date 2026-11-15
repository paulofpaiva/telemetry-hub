using Amazon;
using Amazon.Runtime;
using Amazon.S3;

namespace TelemetryHub.Infrastructure.Storage;

internal static class S3ClientFactory
{
    /// <summary>
    /// Builds a client for AWS S3 or for any S3-compatible endpoint. The difference between the
    /// two is configuration only: service URL, path-style addressing and credentials.
    /// </summary>
    public static IAmazonS3 Create(S3Options options)
    {
        var config = new AmazonS3Config { ForcePathStyle = options.ForcePathStyle };

        if (string.IsNullOrWhiteSpace(options.ServiceUrl))
        {
            config.RegionEndpoint = RegionEndpoint.GetBySystemName(options.Region);
        }
        else
        {
            config.ServiceURL = options.ServiceUrl;
            config.AuthenticationRegion = options.Region;

            // Newer SDK versions add CRC checksums to every request by default, which several
            // S3-compatible servers reject. Only send them when the operation requires it.
            config.RequestChecksumCalculation = RequestChecksumCalculation.WHEN_REQUIRED;
            config.ResponseChecksumValidation = ResponseChecksumValidation.WHEN_REQUIRED;
        }

        return string.IsNullOrEmpty(options.AccessKey)
            ? new AmazonS3Client(config)
            : new AmazonS3Client(new BasicAWSCredentials(options.AccessKey, options.SecretKey), config);
    }
}
