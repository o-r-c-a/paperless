using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Paperless.Contracts.Messages
{
    public class DeleteDocumentIndexMessage
    {
        public Guid Id { get; set; }
    }
}
