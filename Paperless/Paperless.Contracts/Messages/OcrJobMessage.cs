namespace Paperless.Contracts.Messages
{
    public class OcrJobMessage
    {
        public Guid Id { get; init; }
        public string Name { get; init; } = "";
        public string ContentType { get; init; } = "";
        public long SizeBytes { get; init; }
        public DateTime UploadedAt { get; init; }
        public IEnumerable<string> Tags { get; init; } = [];
    }
}
