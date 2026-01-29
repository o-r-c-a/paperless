using Microsoft.EntityFrameworkCore;
using Paperless.Domain.Entities;
using Paperless.Domain.Repositories;
using Paperless.Infrastructure.Persistence;

namespace Paperless.Infrastructure.Repositories
{
    // EF Core implementation for storing daily access aggregates per document.
    // Uses absolute upserts (insert or overwrite).
    public sealed class EfDocumentDailyAccessRepository : IDocumentDailyAccessRepository
    {
        private readonly PaperlessDbContext _db;

        public EfDocumentDailyAccessRepository(PaperlessDbContext db)
        {
            _db = db;
        }

        public async Task UpsertAbsoluteAsync(
            Guid documentId,
            DateTime date,
            string accessType,
            int absoluteCount,
            CancellationToken ct = default)
        {
            if (documentId == Guid.Empty)
                throw new ArgumentException("DocumentId must not be empty.", nameof(documentId));

            if (string.IsNullOrWhiteSpace(accessType))
                throw new ArgumentException("AccessType is required.", nameof(accessType));

            if (absoluteCount < 0)
                throw new ArgumentException("Count must be >= 0.", nameof(absoluteCount));

            // Normalise to UTC date-only boundary (00:00). Matches Domain entity semantics.
            var normalizedDate = NormalizeUtcDateOnly(date);
            var normalizedType = accessType.Trim().ToLowerInvariant();

            var existing = await _db.DocumentDailyAccesses
                .FirstOrDefaultAsync(
                    x => x.DocumentId == documentId
                      && x.Date == normalizedDate
                      && x.AccessType == normalizedType,
                    ct);

            if (existing is null)
            {
                var created = DocumentDailyAccess.Create(documentId, normalizedDate, normalizedType, absoluteCount);
                _db.DocumentDailyAccesses.Add(created);
            }
            else
            {
                existing.SetAbsoluteCount(absoluteCount);
            }

            await _db.SaveChangesAsync(ct);
        }

        public Task<DocumentDailyAccess?> GetAsync(
            Guid documentId,
            DateTime date,
            string accessType,
            CancellationToken ct = default)
        {
            var normalizedDate = NormalizeUtcDateOnly(date);
            var normalizedType = accessType.Trim().ToLowerInvariant();

            return _db.DocumentDailyAccesses
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    x => x.DocumentId == documentId
                      && x.Date == normalizedDate
                      && x.AccessType == normalizedType,
                    ct);
        }

        private static DateTime NormalizeUtcDateOnly(DateTime dt)
        {
            var utc = dt.Kind == DateTimeKind.Utc ? dt : dt.ToUniversalTime();
            return new DateTime(utc.Year, utc.Month, utc.Day, 0, 0, 0, DateTimeKind.Utc);
        }
    }
}
