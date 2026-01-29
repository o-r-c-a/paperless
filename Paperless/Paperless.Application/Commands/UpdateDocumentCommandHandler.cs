using AutoMapper;
using MediatR;
using Microsoft.Extensions.Logging;
using Paperless.Application.DTOs;
using Paperless.Domain.Entities;
using Paperless.Domain.Events;
using Paperless.Domain.Exceptions;
using Paperless.Domain.Repositories;
using Paperless.Domain.ValueObjects;

namespace Paperless.Application.Commands
{
    public class UpdateDocumentCommandHandler : IRequestHandler<UpdateDocumentCommand>
    {
        private readonly IDocumentRepository _repo;
        private readonly IMapper _mapper;
        private readonly ILogger<UpdateDocumentCommandHandler> _logger;
        private readonly IMediator _mediator;

        public UpdateDocumentCommandHandler(
            IDocumentRepository repo,
            IMapper mapper,
            ILogger<UpdateDocumentCommandHandler> logger,
            IMediator mediator)
        {
            _repo = repo;
            _mapper = mapper;
            _logger = logger;
            _mediator = mediator;
        }

        public async Task Handle(UpdateDocumentCommand request, CancellationToken ct)
        {
            _logger.LogInformation("Handling UpdateDocumentCommand for ID: {DocumentId}", request.Id);

            if (!await _repo.ExistsAsync(request.Id, ct))
                throw new DocumentDoesNotExistException(request.Id);

            // Map command to Value Object for Repository
            var updateDto = new UpdateDocumentDTO
            {
                Name = request.Name,
                Tags = request.Tags?.Select(t => new Tag { Name = t }).ToList(),
                Title = request.Title
            };
            var update = _mapper.Map<DocumentUpdate>(updateDto);

            // Update Database
            await _repo.UpdateAsync(request.Id, update, ct);

            // Retrieve updated document to ensure we have latest state for Indexing
            var updatedDoc = await _repo.GetByIdAsync(request.Id, ct);
            if (updatedDoc != null)
            {
                // Publish Domain Event to sync with Elasticsearch
                await _mediator.Publish(new DocumentUpdatedEvent(updatedDoc), ct);
            }
        }
    }
}