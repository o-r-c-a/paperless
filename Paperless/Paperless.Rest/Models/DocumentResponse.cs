namespace Paperless.Rest.Models
{
    public class DocumentResponse
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = "";
        public DateTime UploadedAt { get; set; }
        public string ContentType { get; set; } = "";
        public long SizeBytes { get; set; }
        public IEnumerable<string> Tags { get; set; } = [];
        public string? Title { get; set; }
        //public List<string>? Authors { get; set; }
        public string? Summary { get; set; }
    }
}