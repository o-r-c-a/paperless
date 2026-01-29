using MediatR;
using Microsoft.Extensions.Logging;
using Paperless.Domain.Events;
using Paperless.Domain.Exceptions;
using Paperless.Domain.Repositories;

namespace Paperless.Application.Commands
{
    public class DeleteDocumentCommandHandler : IRequestHandler<DeleteDocumentCommand>
    {
        private readonly IDocumentRepository _repo;
        private readonly ILogger<DeleteDocumentCommandHandler> _logger;
        private readonly IMediator _mediator;

        public DeleteDocumentCommandHandler(IDocumentRepository repo, ILogger<DeleteDocumentCommandHandler> logger, IMediator mediator)
        {
            _repo = repo;
            _logger = logger;
            _mediator = mediator;
        }

        public async Task Handle(DeleteDocumentCommand request, CancellationToken ct)
        {
            _logger.LogInformation("Handling DeleteDocumentCommand for ID: {DocumentId}", request.Id);

            var document = await _repo.GetByIdAsync(request.Id, ct)
                           ?? throw new DocumentDoesNotExistException(request.Id);

            // Delete from Database
            try
            {
                await _repo.DeleteAsync(document, ct);
            }
            catch (Exception ex)
            {
                throw new DocumentDeletionFailedException(request.Id, ex);
            }

            // Publish Domain Event to sync with Elasticsearch
            await _mediator.Publish(new DocumentDeletedEvent(request.Id), ct);
        }
    }
}