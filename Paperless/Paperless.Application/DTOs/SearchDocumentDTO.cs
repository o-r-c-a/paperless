using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Paperless.Application.DTOs
{
    public class SearchDocumentDTO
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public double Score { get; set; }
    }
}
