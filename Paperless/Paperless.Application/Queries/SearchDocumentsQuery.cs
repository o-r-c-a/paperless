using MediatR;
using Paperless.Application.DTOs;

namespace Paperless.Application.Queries
{
    public record SearchDocumentsQuery(string? SearchTerm) : IRequest<IEnumerable<SearchDocumentDTO>>;
}