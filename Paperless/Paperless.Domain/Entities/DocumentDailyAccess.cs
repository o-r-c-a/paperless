using System;

namespace Paperless.Domain.Entities
{
    // Represents the aggregated number of accesses for a single document,
    // per day and per access type (upload/update/download).
    // IMPORTANT: This entity stores DAILY aggregates. Even if XML includes timestamps,
    // the batch processor must aggregate to the day before persisting.
    // AccessType is stored as text (no enums), by design decision.
    public class DocumentDailyAccess
    {
        // Composite key (DocumentId + Date + AccessType) will be configured in EF.
        public Guid DocumentId { get; private set; }

        // Stored as UTC date boundary; time component must be 00:00:00 UTC.
        public DateTime Date { get; private set; }

        public string AccessType { get; private set; } = "";

        public int Count { get; private set; }

        // Parameterless constructor required by EF Core.
        public DocumentDailyAccess() { }

        private DocumentDailyAccess(Guid documentId, DateTime date, string accessType, int count)
        {
            if (documentId == Guid.Empty)
                throw new ArgumentException("DocumentId must not be empty.", nameof(documentId));

            if (string.IsNullOrWhiteSpace(accessType))
                throw new ArgumentException("AccessType is required.", nameof(accessType));

            if (count < 0)
                throw new ArgumentException("Count must be >= 0.", nameof(count));

            DocumentId = documentId;
            Date = EnsureUtcDateOnly(date);
            AccessType = accessType.Trim().ToLowerInvariant();
            Count = count;
        }

        // Factory method to create a daily aggregate row.
        public static DocumentDailyAccess Create(Guid documentId, DateTime date, string accessType, int count)
            => new DocumentDailyAccess(documentId, date, accessType, count);

        // Overwrite the absolute count for this (documentId, date, accessType).
        public void SetAbsoluteCount(int count)
        {
            if (count < 0)
                throw new ArgumentException("Count must be >= 0.", nameof(count));

            Count = count;
        }

        // Ensures the given date is interpreted/stored as a UTC "date-only" boundary (00:00:00).
        // This keeps the DB semantics "daily aggregate" even though we use DateTime for portability.
        private static DateTime EnsureUtcDateOnly(DateTime date)
        {
            var utc = date.Kind == DateTimeKind.Utc ? date : date.ToUniversalTime();
            return new DateTime(utc.Year, utc.Month, utc.Day, 0, 0, 0, DateTimeKind.Utc);
        }
    }
}
