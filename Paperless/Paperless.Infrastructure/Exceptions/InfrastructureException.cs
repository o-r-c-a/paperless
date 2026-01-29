using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Paperless.Infrastructure.Exceptions
{
    public class InfrastructureException : Exception
    {
        public InfrastructureException(string message) : base(message) { }
        public InfrastructureException(string message, Exception? inner) : base(message, inner) { }
    }
}
