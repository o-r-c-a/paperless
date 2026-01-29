using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Paperless.Application.Exceptions
{
    public abstract class ApplicationException : Exception
    {
        protected ApplicationException(string message) : base(message) { }
        protected ApplicationException(string message, Exception? inner) : base(message, inner) { }
    }
}
