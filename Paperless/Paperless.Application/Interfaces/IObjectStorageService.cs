namespace Paperless.Application.Interfaces
{
    // Abstraction for binary object storage (e.g. MinIO, S3, local FS)
    // We keep the application independent from any specific storage SDK
    public interface IObjectStorageService
    {
        // Ensures that the given bucket exists (creates it if necessary)
        Task EnsureBucketAsync(string bucket, CancellationToken ct);

        // Uploads an object to storage
        Task PutObjectAsync(string bucket, string objectName, Stream data, long sizeBytes, string contentType, CancellationToken ct);

        // Downloads an object from storage into the given destination stream
        Task GetObjectAsync(string bucket, string objectName, Stream destination, CancellationToken ct);

        // Deletes an object from storage if it exists
        Task DeleteObjectAsync(string bucket, string objectName, CancellationToken ct);

        // Checks whether an object exists
        Task<bool> ObjectExistsAsync(string bucket, string objectName, CancellationToken ct);
    }
}
