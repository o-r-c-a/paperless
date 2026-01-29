using NSubstitute;
using NUnit.Framework;
using Paperless.Application.DTOs;
using Paperless.Application.Interfaces;
using Paperless.Application.Queries;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Paperless.Tests.Application.Queries
{
    [TestFixture]
    public class SearchDocumentsQueryHandlerTests
    {
        private ISearchRepository _searchRepo;
        private SearchDocumentsQueryHandler _handler;

        [SetUp]
        public void SetUp()
        {
            _searchRepo = Substitute.For<ISearchRepository>();
            _handler = new SearchDocumentsQueryHandler(_searchRepo);
        }

        [Test]
        public async Task Handle_ValidTerm_ReturnsResults()
        {
            // Arrange
            var query = new SearchDocumentsQuery("invoice");
            var results = new List<SearchDocumentDTO> { new() { Name = "invoice.pdf" } };
            _searchRepo.SearchAsync("invoice", Arg.Any<CancellationToken>()).Returns(results);

            // Act
            var result = await _handler.Handle(query, CancellationToken.None);

            // Assert
            Assert.That(result, Is.EqualTo(results));
        }

        [TestCase("")]
        [TestCase("   ")]
        public async Task Handle_Empty_ReturnsEmptyListImmediately(string searchTerm)
        {
            // Act
            var result = await _handler.Handle(new SearchDocumentsQuery(searchTerm), CancellationToken.None);

            // Assert
            Assert.That(result, Is.Empty);
            await _searchRepo.DidNotReceive().SearchAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
        }
    }
}