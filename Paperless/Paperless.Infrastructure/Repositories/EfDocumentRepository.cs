using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Paperless.Domain.Common;
using Paperless.Domain.Entities;
using Paperless.Domain.Repositories;
using Paperless.Domain.ValueObjects;
using Paperless.Infrastructure.Exceptions;
using Paperless.Infrastructure.Persistence;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Paperless.Infrastructure.Repositories
{
    public class EfDocumentRepository : IDocumentRepository
    {
        private readonly PaperlessDbContext _db;
        private readonly ILogger<EfDocumentRepository> _logger;

        public EfDocumentRepository(PaperlessDbContext db, ILogger<EfDocumentRepository> logger)
        {
            _db = db;
            _logger = logger;
        }

        public async Task<Document?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
            await _db.Documents
                .Include(d => d.Tags)
                .AsNoTracking()
                .FirstOrDefaultAsync(d => d.Id == id, ct);

        public async Task<IEnumerable<Document>> GetByNameAsync(string name, CancellationToken ct = default) =>
            await _db.Documents
                .Include(d => d.Tags)
                .AsNoTracking()
                .Where(d => d.Name.Equals(name))
                .ToListAsync(ct);

        public async Task AddAsync(Document doc, CancellationToken ct = default)
        {
            try
            {
                if(doc.Tags.Any())
                { 
                    var tagList = await SaveAndRetrieveTagListAsync(doc.Tags, ct);
                    doc.SetTags(tagList);
                }
                _db.Documents.Add(doc);
                await _db.SaveChangesAsync(ct);
            }
            catch (DbUpdateException ex) when (DbExceptionHelper.IsUniqueConstraintViolation(ex))
            //(ex.InnerException != null)
            {
                // Handle potential unique constraint violations or other DB issues
                throw new InvalidOperationException("Failed to add document to the database.", ex);
            }
        }

        public async Task UpdateAsync(Guid documentId, DocumentUpdate updateDocument, CancellationToken ct = default)
        {

            try
            {
                var trackedEntity = await _db.Documents
                    .Include(d => d.Tags)
                    .FirstOrDefaultAsync(d => d.Id == documentId, ct)
                    ?? throw new InvalidOperationException("Document not found in the database.");

                _logger.LogDebug("Document has following tags before update: " + (!trackedEntity.Tags.Any() ? "empty" : string.Join(", ", trackedEntity.Tags.Select(t => t.Name))));

                if (updateDocument.Name != null)
                    trackedEntity.Name = updateDocument.Name;
                if(updateDocument.Title != null)
                    trackedEntity.Title = updateDocument.Title;

                _logger.LogDebug("Updating document ID " + documentId);
                _logger.LogDebug("New tags are: " + (updateDocument.Tags == null ? "null" : string.Join(", ", updateDocument.Tags.Select(t => t.Name))));

                var ogTagNames = trackedEntity.Tags.Select(t => t.Name).ToList();

                if (updateDocument.Tags != null)
                {
                    _logger.LogDebug("Tags are not null! " + updateDocument);
                    if (updateDocument.Tags.Any())
                    {
                        _logger.LogDebug("Tags are not empty! " + updateDocument.Tags.Count());
                        var updatedTags = await SaveAndRetrieveTagListAsync(updateDocument.Tags, ct);
                        trackedEntity.SetTags(updatedTags);
                    }
                    else
                    {
                        _logger.LogDebug("Tags are empty, clearing tags! " + documentId);
                        trackedEntity.SetTags([]);
                    }
                }
                var newTagNames = trackedEntity.Tags.Select(t => t.Name).ToList();
                var candidateOrphanTagNames = ogTagNames.Except(newTagNames).ToList();
                if (candidateOrphanTagNames.Count != 0)
                    await MarkPossibleOrphanTagsForDeletionAsync(documentId, candidateOrphanTagNames, ct);

                await _db.SaveChangesAsync(ct);
            }
            catch (DbUpdateConcurrencyException ex)
            {
                // Handle concurrency issues
                throw new InvalidOperationException("The document was modified by another process.", ex);
            }
            catch (DbUpdateException ex) when (ex.InnerException != null)
            {
                // Handle other DB issues
                throw new InvalidOperationException("Failed to update document in the database.", ex);
            }
        }

        public async Task<IReadOnlyList<Tag?>> GetTagsByIdAsync(Guid id, CancellationToken ct = default)
        {
            var document = await _db.Documents
                .Include(d => d.Tags)
                .AsNoTracking()
                .FirstOrDefaultAsync(d => d.Id == id, ct)
                ?? throw new KeyNotFoundException($"Document with id {id} not found.");

            return [.. document.Tags];
        }

        public async Task DeleteAsync(Document doc, CancellationToken ct = default)
        {
            var candidateOrphanTagNames = doc.Tags.Select(t => t.Name).ToList();
            if (candidateOrphanTagNames.Count != 0)
                await MarkPossibleOrphanTagsForDeletionAsync(doc.Id, candidateOrphanTagNames, ct);
            _db.Documents.Remove(doc);
            await _db.SaveChangesAsync(ct);
        }

        public async Task UpdateSummaryAsync(Guid id, string summary, CancellationToken ct = default)
        {
            var entity = await _db.Documents.FirstOrDefaultAsync(d => d.Id == id, ct);
            if (entity is null)
            {
                // Note Chava: We can use a domain-specific exception later;
                // for now a KeyNotFoundException will do.
                throw new KeyNotFoundException($"Document with id {id} not found.");
            }

            entity.SetSummary(summary);
            await _db.SaveChangesAsync(ct);
        }

        public async Task<bool> ExistsAsync(Guid id, CancellationToken ct = default) =>
            await _db.Documents.AnyAsync(d => d.Id == id, ct);

        public async Task<IReadOnlyList<Document>> ListAsync(int skip = 0, int take = 50, CancellationToken ct = default) =>
            await _db.Documents.AsNoTracking()
                               .Include(d => d.Tags)
                               .OrderByDescending(d => d.UploadedAt)
                               .Skip(skip)
                               .Take(take)
                               .ToListAsync(ct);

        public async Task CleanupOrphanedTagsAsync(CancellationToken ct = default)
        {
            var orphanedTags = await _db.Tags
                .Where(t => !t.Documents.Any())
                .ToListAsync(ct);
            if (orphanedTags.Count != 0)
            {
                _logger.LogDebug("Cleaning up "
                    + orphanedTags.Count
                    + " unused tags: "
                    + string.Join(", ", orphanedTags.Select(t => t.Name)));
                _db.Tags.RemoveRange(orphanedTags);
                await _db.SaveChangesAsync(ct);
            }
        }
        private async Task MarkPossibleOrphanTagsForDeletionAsync(Guid documentID, IEnumerable<string> possibleOrphanTagNames, CancellationToken ct = default)
        {
            try
            {
                _logger.LogDebug("Checking for orphaned tags to clean up...");
                var orphanedTags = await _db.Tags
                    .Where(t => possibleOrphanTagNames.Contains(t.Name))
                    .Where(t => !t.Documents.Any(d => d.Id != documentID))
                    .ToListAsync(ct);
                if (orphanedTags.Count != 0)
                {
                    _logger.LogDebug("Cleaning up "
                        + orphanedTags.Count
                        + " unused tags: "
                        + string.Join(", ", orphanedTags.Select(t => t.Name)));
                    _db.Tags.RemoveRange(orphanedTags);
                }
            }
            catch (DbUpdateException ex) when (ex.InnerException != null)
            {
                // Handle potential unique constraint violations or other DB issues
                throw new InvalidOperationException("Failed to clean up orphaned tags in the database.", ex);
            }
        }

        private async Task<IEnumerable<Tag>> SaveAndRetrieveTagListAsync(IEnumerable<Tag> tags, CancellationToken ct = default)
        {
            var existingTags = await RetrieveExistingTagsAsync(tags, ct);
            Debug.WriteLine("Existing tags: " + string.Join(", ", existingTags.Select(t => t.Name)));
            var missingTags = tags
                .Where(t => !existingTags.Any(et => et.Name == t.Name))
                .ToList();
            Debug.WriteLine("Missing tags: " + string.Join(", ", missingTags.Select(t => t.Name)));
            var newTags = CreateMissingTags(missingTags, ct);
            Debug.WriteLine("Newly created tags: " + string.Join(", ", newTags.Select(t => t.Name)));
            return [.. existingTags, .. newTags];
        }

        private async Task<IEnumerable<Tag>> RetrieveExistingTagsAsync(IEnumerable<Tag> tags, CancellationToken ct = default) =>
            await _db.Tags
                .Where(t => tags.Select(tag => tag.Name).Contains(t.Name))
                .ToListAsync(ct);

        private List<Tag> CreateMissingTags(IEnumerable<Tag> missingTags, CancellationToken ct = default)
        {
            if (!missingTags.Any()) return [];
            var newTags = missingTags.Select(mt => new Tag { Name = mt.Name }).ToList();
            _db.Tags.AddRange(newTags);
            return newTags;
        }
    }
}
