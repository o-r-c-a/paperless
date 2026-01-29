using AutoMapper;
using MediatR;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Paperless.Application.Commands;
using Paperless.Application.DTOs;
using Paperless.Domain.Entities;
using Paperless.Domain.Events;
using Paperless.Domain.Exceptions;
using Paperless.Domain.Repositories;
using Paperless.Domain.ValueObjects;

namespace Paperless.Tests.Application.Commands
{
    [TestFixture]
    public class UpdateDocumentCommandHandlerTests
    {
        private IDocumentRepository _repo;
        private IMapper _mapper;
        private ILogger<UpdateDocumentCommandHandler> _logger;
        private IMediator _mediator;
        private UpdateDocumentCommandHandler _handler;

        [SetUp]
        public void SetUp()
        {
            _repo = Substitute.For<IDocumentRepository>();
            _mapper = Substitute.For<IMapper>();
            _logger = Substitute.For<ILogger<UpdateDocumentCommandHandler>>();
            _mediator = Substitute.For<IMediator>();
            _handler = new UpdateDocumentCommandHandler(_repo, _mapper, _logger, _mediator);
        }

        [Test]
        public async Task Handle_DocumentExists_UpdatesAndPublishesEvent()
        {
            // Arrange
            var id = Guid.NewGuid();
            var command = new UpdateDocumentCommand { Id = id, Name = "New Name" };
            var docEntity = Document.Create("Old Name", "pdf", 100);

            _repo.ExistsAsync(id, Arg.Any<CancellationToken>()).Returns(true);
            _mapper.Map<DocumentUpdate>(Arg.Any<UpdateDocumentDTO>()).Returns(new DocumentUpdate());

            // Mock fetching the "updated" doc for event publishing
            _repo.GetByIdAsync(id, Arg.Any<CancellationToken>()).Returns(docEntity);

            // Act
            await _handler.Handle(command, CancellationToken.None);

            // Assert
            await _repo.Received(1).UpdateAsync(id, Arg.Any<DocumentUpdate>(), Arg.Any<CancellationToken>());
            await _mediator.Received(1).Publish(Arg.Any<DocumentUpdatedEvent>(), Arg.Any<CancellationToken>());
        }

        [Test]
        public void Handle_DocumentDoesNotExist_ThrowsException()
        {
            // Arrange
            var id = Guid.NewGuid();
            _repo.ExistsAsync(id, Arg.Any<CancellationToken>()).Returns(false);

            // Act & Assert
            Assert.ThrowsAsync<DocumentDoesNotExistException>(async () =>
                await _handler.Handle(new UpdateDocumentCommand { Id = id }, CancellationToken.None));

            _repo.DidNotReceive().UpdateAsync(Arg.Any<Guid>(), Arg.Any<DocumentUpdate>(), Arg.Any<CancellationToken>());
        }
    }
}