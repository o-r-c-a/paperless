namespace Paperless.Contracts.Messages
{
    public class OcrResultMessage
    {
        public Guid Id { get; init; }              // Document ID
        public string Name { get; init; } = "";    // Document name
        public string ContentType { get; init; } = "";
        public long SizeBytes { get; init; }
        public DateTime UploadedAt { get; init; }

        // Full OCR text (may be long)
        public string Text { get; init; } = "";
        public IEnumerable<string> Tags { get; set; } = [];
    }
}
