using AutoMapper;
using MediatR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Paperless.Application.DTOs;
using Paperless.Application.Interfaces;
using Paperless.Contracts.Options;
using Paperless.Domain.Entities;
using Paperless.Domain.Repositories;

namespace Paperless.Application.Commands
{
    public class UploadDocumentCommandHandler : IRequestHandler<UploadDocumentCommand, DocumentDTO>
    {
        private readonly IDocumentRepository _repo;
        private readonly IObjectStorageService _storage;
        private readonly IMapper _mapper;
        private readonly ILogger<UploadDocumentCommandHandler> _logger;
        private readonly MinioOptions _minioOptions;
        private readonly IMediator _mediator;

        public UploadDocumentCommandHandler(
            IDocumentRepository repo,
            IObjectStorageService storage,
            IMapper mapper,
            ILogger<UploadDocumentCommandHandler> logger,
            IOptions<MinioOptions> minioOptions,
            IMediator mediator)
        {
            _repo = repo;
            _storage = storage;
            _mapper = mapper;
            _logger = logger;
            _minioOptions = minioOptions.Value;
            _mediator = mediator;
        }

        public async Task<DocumentDTO> Handle(UploadDocumentCommand request, CancellationToken ct)
        {
            _logger.LogInformation("Handling UploadDocumentCommand for {FileName}", request.File.FileName);

            var docEntity = Document.Create(
                name: request.Name,
                contentType: request.File.ContentType,
                sizeBytes: request.File.Length,
                tags: request.Tags,
                title: request.Title
            );

            await _repo.AddAsync(docEntity, ct);

            // MinIO with compensation
            var ext = Path.GetExtension(request.File.FileName);
            var objectName = $"{docEntity.Id}{ext}";

            try
            {
                await using var stream = request.File.OpenReadStream();
                await _storage.PutObjectAsync(
                    _minioOptions.Bucket,
                    objectName,
                    stream,
                    request.File.Length,
                    request.File.ContentType,
                    ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to upload to storage. Compensating by deleting DB entry.");
                await _repo.DeleteAsync(docEntity, ct);
                throw; // Rethrow to let controller know it failed
            }

            // Dispatch Domain Events
            foreach (var domainEvent in docEntity.DomainEvents)
            {
                await _mediator.Publish(domainEvent, ct);
            }
            // optional:
            // docEntity.ClearDomainEvents();

            return _mapper.Map<DocumentDTO>(docEntity);
        }
    }
}