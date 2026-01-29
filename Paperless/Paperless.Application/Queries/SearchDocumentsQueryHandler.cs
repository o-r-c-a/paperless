using MediatR;
using Paperless.Application.DTOs;
using Paperless.Application.Interfaces;

namespace Paperless.Application.Queries
{
    public class SearchDocumentsQueryHandler : IRequestHandler<SearchDocumentsQuery, IEnumerable<SearchDocumentDTO>>
    {
        private readonly ISearchRepository _searchRepository;

        public SearchDocumentsQueryHandler(ISearchRepository searchRepository)
        {
            _searchRepository = searchRepository;
        }

        public async Task<IEnumerable<SearchDocumentDTO>> Handle(SearchDocumentsQuery request, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(request.SearchTerm))
            {
                return [];
            }

            return await _searchRepository.SearchAsync(request.SearchTerm, ct);
        }
    }
}