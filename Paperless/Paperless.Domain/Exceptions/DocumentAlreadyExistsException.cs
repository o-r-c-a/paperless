using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Paperless.Domain.Exceptions
{
    public class DocumentAlreadyExistsException : DomainException
    {
        public string DocumentName { get; set; }
        public DocumentAlreadyExistsException(string documentName) : base
            ($"A document with name {documentName} already exists!")
        {
            DocumentName = documentName;
        }
    }
}
