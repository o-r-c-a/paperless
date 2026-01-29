namespace Paperless.Contracts.Options
{
    // Configuration settings for connecting to MinIO
    public class MinioOptions
    {
        public const string SectionName = "Minio";
        // Endpoint host name
        public string Endpoint { get; set; } = string.Empty;

        // Port number
        public int Port { get; set; }

        // Access key for MinIO login
        public string AccessKey { get; set; } = string.Empty;

        // Secret key for MinIO login
        public string SecretKey { get; set; } = string.Empty;

        public string UseSSL { get; set; } = string.Empty;

        // Default bucket for file storage
        public string Bucket { get; set; } = string.Empty;
    }
}
