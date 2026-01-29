using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Paperless.Domain.Entities;
using Paperless.Domain.ValueObjects;

namespace Paperless.Domain.Repositories
{
    public interface IDocumentRepository
    {
        Task<Document?> GetByIdAsync(Guid id, CancellationToken ct = default);
        Task<IEnumerable<Document>> GetByNameAsync(string name, CancellationToken ct = default);
        Task AddAsync(Document doc, CancellationToken ct = default);
        Task UpdateAsync(Guid documentId, DocumentUpdate updateDocument, CancellationToken ct = default);
        Task<IReadOnlyList<Tag?>> GetTagsByIdAsync(Guid id, CancellationToken ct = default);
        Task DeleteAsync(Document doc, CancellationToken ct = default);
        Task UpdateSummaryAsync(Guid id, string summary, CancellationToken ct = default);

        // Optional (maybe we'll need it later):
        Task<bool> ExistsAsync(Guid id, CancellationToken ct = default);
        Task<IReadOnlyList<Document>> ListAsync(int skip = 0, int take = 50, CancellationToken ct = default);
        Task CleanupOrphanedTagsAsync(CancellationToken ct = default);
    }
}
