using AutoMapper;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Paperless.Application.DTOs;
using Paperless.Application.Services;
using Paperless.Domain.Entities;
using Paperless.Domain.Exceptions;
using Paperless.Domain.Repositories;

namespace Paperless.Tests.Application.Services
{
    [TestFixture]
    public class DocumentsServiceTests
    {
        private IDocumentRepository _repo;
        private IMapper _mapper;
        private ILogger<DocumentService> _logger;
        private DocumentService _documentService;

        [SetUp]
        public void SetUp()
        {
            _repo = Substitute.For<IDocumentRepository>();
            _mapper = Substitute.For<IMapper>();
            _logger = Substitute.For<ILogger<DocumentService>>();
            _documentService = new DocumentService(_repo, _mapper, _logger);
        }

        #region GetDocumentByIdAsync

        [Test]
        public async Task GetDocumentByIdAsync_WhenDocumentExists_ReturnsDto()
        {
            // Arrange
            var id = Guid.NewGuid();
            var doc = Document.Create("test.pdf", "application/pdf", 100);
            var expectedDto = new DocumentDTO { Id = id, Name = "test.pdf" };

            _repo.GetByIdAsync(id, Arg.Any<CancellationToken>()).Returns(doc);
            _mapper.Map<DocumentDTO>(doc).Returns(expectedDto);

            // Act
            var result = await _documentService.GetDocumentByIdAsync(id, CancellationToken.None);

            // Assert
            Assert.That(result, Is.EqualTo(expectedDto));
            await _repo.Received(1).GetByIdAsync(id, Arg.Any<CancellationToken>());
        }

        [Test]
        public void GetDocumentByIdAsync_WhenDocumentDoesNotExist_ThrowsDocumentDoesNotExistException()
        {
            // Arrange
            var id = Guid.NewGuid();
            _repo.GetByIdAsync(id, Arg.Any<CancellationToken>()).Returns((Document?)null);

            // Act & Assert
            Assert.ThrowsAsync<DocumentDoesNotExistException>(async () =>
                await _documentService.GetDocumentByIdAsync(id, CancellationToken.None));
        }

        #endregion

        #region SetSummaryAsync

        [Test]
        public async Task SetSummaryAsync_WhenValidRequest_UpdatesSummary()
        {
            // Arrange
            var id = Guid.NewGuid();
            var summary = "This is a summary";

            _repo.ExistsAsync(id, Arg.Any<CancellationToken>()).Returns(true);

            // Act
            await _documentService.SetSummaryAsync(id, summary, CancellationToken.None);

            // Assert
            await _repo.Received(1).UpdateSummaryAsync(id, summary, Arg.Any<CancellationToken>());
        }

        [Test]
        public async Task SetSummaryAsync_WhenSummaryIsEmpty_DoesNotCallUpdate()
        {
            // Arrange
            var id = Guid.NewGuid();
            var summary = "   "; // whitespace

            // Act
            await _documentService.SetSummaryAsync(id, summary, CancellationToken.None);

            // Assert
            await _repo.DidNotReceive().ExistsAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
            await _repo.DidNotReceive().UpdateSummaryAsync(Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
        }

        [Test]
        public void SetSummaryAsync_WhenDocumentDoesNotExist_ThrowsException()
        {
            // Arrange
            var id = Guid.NewGuid();
            var summary = "Valid Summary";

            _repo.ExistsAsync(id, Arg.Any<CancellationToken>()).Returns(false);

            // Act & Assert
            Assert.ThrowsAsync<DocumentDoesNotExistException>(async () =>
                await _documentService.SetSummaryAsync(id, summary, CancellationToken.None));

            _repo.DidNotReceive().UpdateSummaryAsync(Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
        }

        #endregion

        #region ListDocumentsAsync

        [Test]
        public async Task ListDocumentsAsync_WithDocuments_ReturnsMappedDtos()
        {
            // Arrange
            var docs = new List<Document>
            {
                Document.Create("a.pdf", "application/pdf", 10),
                Document.Create("b.pdf", "application/pdf", 20)
            };
            var expectedDtos = new List<DocumentDTO>
            {
                new DocumentDTO { Name = "a.pdf" },
                new DocumentDTO { Name = "b.pdf" }
            };

            _repo.ListAsync(0, 50, Arg.Any<CancellationToken>()).Returns(docs);
            _mapper.Map<IReadOnlyList<DocumentDTO>>(docs).Returns(expectedDtos);

            // Act
            var result = await _documentService.ListDocumentsAsync(0, 50, CancellationToken.None);

            // Assert
            Assert.That(result, Is.EquivalentTo(expectedDtos));
            await _repo.Received(1).ListAsync(0, 50, Arg.Any<CancellationToken>());
        }

        [Test]
        public async Task ListDocumentsAsync_WithNoDocuments_ReturnsEmptyList()
        {
            // Arrange
            var docs = new List<Document>();
            var expectedDtos = new List<DocumentDTO>();

            _repo.ListAsync(0, 50, Arg.Any<CancellationToken>()).Returns(docs);
            _mapper.Map<IReadOnlyList<DocumentDTO>>(docs).Returns(expectedDtos);

            // Act
            var result = await _documentService.ListDocumentsAsync(0, 50, CancellationToken.None);

            // Assert
            Assert.That(result, Is.Empty);
        }

        #endregion
    }
}