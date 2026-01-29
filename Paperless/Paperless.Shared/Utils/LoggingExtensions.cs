using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Paperless.Shared.Utils
{
    public static class LoggingExtensions
    {
        public static LoggerConfiguration WithPaperlessDefaults(this LoggerConfiguration cfg)
        {
            return cfg.Enrich.FromLogContext()
                      .Enrich.With<ShortSourceContextEnricher>();
        }
    }

}
