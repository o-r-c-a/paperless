using System.ComponentModel.DataAnnotations;

namespace Paperless.Rest.Models
{
    public class CreateDocumentRequest
    {
        [Required]
        public string Name { get; set; } = "";
        [Required]
        public IFormFile File { get; set; } = null!;
        public IEnumerable<string>? Tags { get; set; }
        public string? Title { get; set; }
        //public List<string>? Authors { get; set; }
    }
}
