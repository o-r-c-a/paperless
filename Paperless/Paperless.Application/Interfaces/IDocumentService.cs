using Paperless.Application.DTOs;
using Paperless.Domain.Entities;

namespace Paperless.Application.Interfaces
{
    public interface IDocumentService
    {
        Task<DocumentDTO> GetDocumentByIdAsync(Guid id, CancellationToken ct = default);
        Task<IReadOnlyList<DocumentDTO>> ListDocumentsAsync(int skip = 0, int take = 50, CancellationToken ct = default);
        Task SetSummaryAsync(Guid id, string summary, CancellationToken ct = default);
    }
}