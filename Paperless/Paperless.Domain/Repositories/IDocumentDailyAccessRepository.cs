using System;
using System.Threading;
using System.Threading.Tasks;
using Paperless.Domain.Entities;

namespace Paperless.Domain.Repositories
{
    // Persistence boundary for daily access aggregates.
    // The batch processor uses this repository to upsert absolute counts.
    public interface IDocumentDailyAccessRepository
    {
        // Insert the record if it doesn't exist, otherwise overwrite the count.
        // The date must represent the day (00:00 UTC); implementations should normalise defensively.
        Task UpsertAbsoluteAsync(
            Guid documentId,
            DateTime date,
            string accessType,
            int absoluteCount,
            CancellationToken ct = default);

        // Helper for tests/manual verification.
        Task<DocumentDailyAccess?> GetAsync(
            Guid documentId,
            DateTime date,
            string accessType,
            CancellationToken ct = default);
    }
}
