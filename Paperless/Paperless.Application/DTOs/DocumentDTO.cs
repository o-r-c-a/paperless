using Paperless.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Paperless.Application.DTOs
{
    public class DocumentDTO
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = "";
        public DateTime UploadedAt { get; set; }
        public string ContentType { get; set; } = "";
        public long SizeBytes { get; set; }
        public List<Tag> Tags { get; set; } = [];
        public string? Title { get; set; }
        //public List<string>? Authors { get; set; }
        public string? Summary { get; set; }
    }
}
