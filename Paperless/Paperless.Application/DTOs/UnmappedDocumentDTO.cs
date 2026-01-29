using Paperless.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Paperless.Application.DTOs
{
    public class UnmappedDocumentDTO
    {
        public string Name { get; set; } = "";
        public string ContentType { get; set; } = "";
        public long SizeBytes { get; set; }
        public IEnumerable<Tag>? Tags { get; set; }
        public string? Title { get; set; }
        //public List<string>? Authors { get; set; }
    }
}
