using Serilog.Core;
using Serilog.Events;

namespace Paperless.Shared.Utils
{
    public class ShortSourceContextEnricher : ILogEventEnricher
    {
        public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
        {
            if (logEvent.Properties.TryGetValue("SourceContext", out var value))
            {
                var fullName = value.ToString().Trim('"');
                var shortName = fullName.Split('.').Last(); // take only class name

                var shortProp = propertyFactory.CreateProperty("SourceContext", shortName);
                logEvent.AddOrUpdateProperty(shortProp);
            }
        }
    }

}
