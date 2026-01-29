using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Paperless.Contracts.Options
{
    public class GeminiOptions
    {
        public const string SectionName = "Gemini";

        public string ApiKey { get; set; } = string.Empty;
        public string Model { get; set; } = "gemini-2.5-flash";
        // Added Retry Logic
        public int MaxRetries { get; set; } = 3;
        public int RetryDelayMilliseconds { get; set; } = 2000;
    }
}
