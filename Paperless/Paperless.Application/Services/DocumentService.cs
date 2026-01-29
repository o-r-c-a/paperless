using AutoMapper;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Paperless.Application.DTOs;
using Paperless.Application.Interfaces;
using Paperless.Domain.Entities;
using Paperless.Domain.Exceptions;
using Paperless.Domain.Repositories;
using Paperless.Domain.ValueObjects;
using System.Diagnostics;

namespace Paperless.Application.Services
{
    public class DocumentService : IDocumentService
    {
        private readonly IDocumentRepository _repo;
        private readonly IMapper _mapper;
        private readonly ILogger<DocumentService> _logger;
        public DocumentService
            (
            IDocumentRepository repo,
            IMapper mapper,
            ILogger<DocumentService> logger
            )
        {
            _repo = repo;
            _mapper = mapper;
            _logger = logger;
        }

        public async Task<DocumentDTO> GetDocumentByIdAsync(Guid id, CancellationToken ct = default)
        {
            _logger.LogInformation("Retrieving document with ID: {DocumentId}", id);
            try
            {
                var document = await _repo.GetByIdAsync(id, ct) ?? throw new DocumentDoesNotExistException(id);
                var docDto = _mapper.Map<DocumentDTO>(document);
                return docDto;
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogWarning(ex, "Document with ID {DocumentId} does not exist.", id);
                throw new DocumentDoesNotExistException(id);
            }
        }

        public async Task SetSummaryAsync(Guid id, string summary, CancellationToken ct = default)
        {
            _logger.LogInformation("Updating summary for document {DocumentId}", id);

            if (string.IsNullOrWhiteSpace(summary))
            {
                _logger.LogWarning("Empty summary provided for document {DocumentId}. Skipping update.", id);
                return;
            }

            if (!await _repo.ExistsAsync(id, ct))
            {
                _logger.LogWarning("Document with ID {DocumentId} does not exist when setting summary.", id);
                throw new DocumentDoesNotExistException(id);
            }

            await _repo.UpdateSummaryAsync(id, summary, ct);
        }

        public async Task<IReadOnlyList<DocumentDTO>> ListDocumentsAsync(int skip = 0, int take = 50, CancellationToken ct = default)
        {
            var documents = await _repo.ListAsync(skip, take, ct);
            var docDto = _mapper.Map<IReadOnlyList<DocumentDTO>>(documents);
            return docDto;
        }
    }
}
