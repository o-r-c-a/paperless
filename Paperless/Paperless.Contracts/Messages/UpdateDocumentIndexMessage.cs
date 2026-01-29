using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Paperless.Contracts.Messages
{
    public class UpdateDocumentIndexMessage
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Title { get; set; }
        public IEnumerable<string> Tags { get; set; } = [];
    }
}
