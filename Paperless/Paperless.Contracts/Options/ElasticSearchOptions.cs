using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Paperless.Contracts.Options
{
    public class ElasticSearchOptions
    {
        public const string SectionName = "ElasticSearch";

        // Default values ensure it works locally without config
        public string Url { get; set; } = "http://elasticsearch:9200";
        public string IndexName { get; set; } = "documents";
    }
}
