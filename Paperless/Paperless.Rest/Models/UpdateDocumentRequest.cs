namespace Paperless.Rest.Models
{
    public class UpdateDocumentRequest
    {
        public string? Name { get; set; }
        public IEnumerable<string>? Tags { get; set; }
        public string? Title { get; set; }
        //public List<string>? Authors { get; set; }
    }
}