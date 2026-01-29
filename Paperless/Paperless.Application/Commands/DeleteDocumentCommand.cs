using MediatR;

namespace Paperless.Application.Commands
{
    public record DeleteDocumentCommand(Guid Id) : IRequest;
}