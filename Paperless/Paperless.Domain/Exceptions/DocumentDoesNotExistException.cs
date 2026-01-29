using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Paperless.Domain.Exceptions
{
    public class DocumentDoesNotExistException : DomainException
    {
        public string DocumentId { get; set; }
        public DocumentDoesNotExistException(Guid documentId, Exception? inner = null) : base
            ($"A document with id {documentId} does not exist!", inner)
        {
            DocumentId = documentId.ToString();
        }
    }
}
