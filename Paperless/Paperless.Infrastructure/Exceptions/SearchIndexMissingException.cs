using Paperless.Domain.Exceptions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Paperless.Infrastructure.Exceptions
{
    public class SearchIndexMissingException : InfrastructureException
    {
        public string IndexName { get; }

        public SearchIndexMissingException(string indexName, Exception? inner = null)
            : base($"The search index '{indexName}' is missing. Search functionality is currently unavailable.", inner)
        {
            IndexName = indexName;
        }
    }
}
