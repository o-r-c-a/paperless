using Microsoft.Extensions.Logging;
using Minio;
using Minio.DataModel.Args;
using Minio.Exceptions;
using Paperless.Application.Interfaces;
using Paperless.Contracts.Options;
using Paperless.Infrastructure.Exceptions;

namespace Paperless.Infrastructure.Storage
{
    // MinIO-backed implementation of IObjectStorageService
    public class MinioStorageService : IObjectStorageService
    {
        private readonly IMinioClient _minio;
        private readonly ILogger<MinioStorageService> _logger;

        public MinioStorageService(
            IMinioClient minio,
            ILogger<MinioStorageService> logger)
        {
            _minio = minio;
            _logger = logger;
        }

        // Ensures that the given bucket exists (creates it if necessary) - happens now once at startup!
        public async Task EnsureBucketAsync(string bucket, CancellationToken ct)
        {
            try
            {
                var exists = await _minio.BucketExistsAsync(
                    new BucketExistsArgs().WithBucket(bucket), ct);
                if (!exists)
                {
                    _logger.LogInformation("Bucket {BucketName} does not exist. Creating...", bucket);
                    var mbArgs = new MakeBucketArgs().WithBucket(bucket);
                    await _minio.MakeBucketAsync(mbArgs, ct);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error ensuring bucket {BucketName} exists.", bucket);
                throw new InfrastructureException($"Could not ensure bucket {bucket}", ex);
            }
        }

        // Uploads an object to storage
        public async Task PutObjectAsync(string bucket, string objectName, Stream data, long sizeBytes, string contentType, CancellationToken ct)
        {
            //await EnsureBucketAsync(bucket, ct);
            try
            {
                var putArgs = new PutObjectArgs()
                    .WithBucket(bucket)
                    .WithObject(objectName)
                    .WithStreamData(data)
                    .WithObjectSize(sizeBytes)
                    .WithContentType(string.IsNullOrWhiteSpace(contentType) ? "application/octet-stream" : contentType);

                await _minio.PutObjectAsync(putArgs, ct);
                _logger.LogInformation("Uploaded object {Object} to MinIO bucket {Bucket} ({Size} bytes)", objectName, bucket, sizeBytes);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error uploading object {Object} to bucket {Bucket}", objectName, bucket);
                throw new InfrastructureException($"Could not upload object {objectName} to bucket {bucket}", ex);
            }
        }

        // Downloads an object from storage into the given destination stream
        public async Task GetObjectAsync(string bucket, string objectName, Stream destination, CancellationToken ct)
        {
            try
            {
                var getArgs = new GetObjectArgs()
                .WithBucket(bucket)
                .WithObject(objectName)
                .WithCallbackStream(async s =>
                {
                    // Copy the downloaded stream to the provided destination stream
                    await s.CopyToAsync(destination, 81920, ct);
                    await destination.FlushAsync(ct);
                });

                await _minio.GetObjectAsync(getArgs, ct);
                _logger.LogInformation("Downloaded object {Object} from MinIO bucket {Bucket}", objectName, bucket);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error downloading object {Object} from bucket {Bucket}", objectName, bucket);
                throw new InfrastructureException($"Could not download object {objectName} from bucket {bucket}", ex);
            }
        }

        // Deletes an object from storage if it exists
        public async Task DeleteObjectAsync(string bucket, string objectName, CancellationToken ct)
        {
            // If it does not exist, remove is effectively a no-op
            var exists = await ObjectExistsAsync(bucket, objectName, ct);
            if (!exists)
            {
                _logger.LogInformation("Object {Object} not found in MinIO bucket {Bucket}; nothing to delete", objectName, bucket);
                return;
            }

            try
            {
                var removeArgs = new RemoveObjectArgs()
                    .WithBucket(bucket)
                    .WithObject(objectName);
                await _minio.RemoveObjectAsync(removeArgs, ct);

                _logger.LogInformation("Deleted object {Object} from MinIO bucket {Bucket}", objectName, bucket);
            }
            catch (Exception ex) {
                _logger.LogError(ex, "Error deleting object {Object} from bucket {Bucket}", objectName, bucket);
                throw new InfrastructureException($"Could not delete object {objectName} from bucket {bucket}", ex);
            }
        }

        // Checks whether an object exists
        public async Task<bool> ObjectExistsAsync(string bucket, string objectName, CancellationToken ct)
        {
            try
            {
                var statArgs = new StatObjectArgs()
                    .WithBucket(bucket)
                    .WithObject(objectName);

                await _minio.StatObjectAsync(statArgs, ct);
                return true;
            }
            catch (MinioException ex)
            {
                // Stat will throw for "not found" and other errors; treat only not-found as false
                // Other errors should be logged and rethrown to avoid hiding issues
                if (ex.Message != null && ex.Message.Contains("Not found", System.StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }

                _logger.LogError(ex, "Error checking existence of object {Object} in bucket {Bucket}", objectName, bucket);
                throw;
            }
        }
    }
}
