using System.Globalization;
using System.Xml.Linq;

namespace Paperless.BatchAccess;

public sealed class AccessLogXmlParser
{
    private static readonly HashSet<string> AllowedTypes =
    [
        "upload",
        "update",
        "download"
    ];

    // Parses the XML and returns daily aggregates:
    // (documentId, dateUtc, accessType) -> count
    public Dictionary<(Guid DocumentId, DateTime DateUtc, string AccessType), int> ParseAndAggregate(string xmlPath)
    {
        var doc = XDocument.Load(xmlPath);
        if (doc.Root is null)
            throw new FormatException("XML has no root element.");

        if (!string.Equals(doc.Root.Name.LocalName, "accessStatistics", StringComparison.OrdinalIgnoreCase))
            throw new FormatException("Root element must be <accessStatistics>.");

        var result = new Dictionary<(Guid, DateTime, string), int>();

        foreach (var ev in doc.Root.Elements("event"))
        {
            var docIdStr = (string?)ev.Attribute("documentId");
            var typeStr = ((string?)ev.Attribute("type"))?.Trim().ToLowerInvariant();
            var atStr = (string?)ev.Attribute("at");

            if (string.IsNullOrWhiteSpace(docIdStr))
                throw new FormatException("Missing attribute 'documentId' on <event>.");

            if (!Guid.TryParse(docIdStr, out var docId))
                throw new FormatException($"Invalid documentId: '{docIdStr}'.");

            if (string.IsNullOrWhiteSpace(typeStr))
                throw new FormatException("Missing attribute 'type' on <event>.");

            if (!AllowedTypes.Contains(typeStr))
                throw new FormatException($"Unsupported access type '{typeStr}'. Allowed: upload, update, download.");

            if (string.IsNullOrWhiteSpace(atStr))
                throw new FormatException("Missing attribute 'at' on <event>.");

            // Expect ISO-8601 with timezone (Z or +xx:yy). Normalise to UTC date boundary.
            if (!DateTimeOffset.TryParse(atStr, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var dto))
                throw new FormatException($"Invalid timestamp '{atStr}'. Expected ISO-8601, e.g. 2026-01-10T10:15:00Z.");

            var utc = dto.ToUniversalTime();
            var dateUtc = new DateTime(utc.Year, utc.Month, utc.Day, 0, 0, 0, DateTimeKind.Utc);

            var key = (docId, dateUtc, typeStr);
            result[key] = result.TryGetValue(key, out var current) ? current + 1 : 1;
        }

        return result;
    }
}
