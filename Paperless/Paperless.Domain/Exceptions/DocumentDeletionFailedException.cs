using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Paperless.Domain.Exceptions
{
    public class DocumentDeletionFailedException : DomainException
    {
        public string DocumentId { get; set; }
        public DocumentDeletionFailedException(Guid documentId, Exception? inner = null) : base
            ($"Document with id {documentId} could not be deleted!", inner)
        {
            DocumentId = documentId.ToString();
        }
    }
}
