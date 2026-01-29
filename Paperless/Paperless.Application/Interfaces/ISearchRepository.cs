using Paperless.Application.DTOs;

namespace Paperless.Application.Interfaces
{
    public interface ISearchRepository
    {
        Task<IEnumerable<SearchDocumentDTO>> SearchAsync(string searchTerm, CancellationToken ct = default);
    }
}