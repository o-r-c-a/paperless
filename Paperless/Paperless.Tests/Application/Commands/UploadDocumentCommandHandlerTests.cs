using AutoMapper;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using Paperless.Application.Commands;
using Paperless.Application.DTOs;
using Paperless.Application.Interfaces;
using Paperless.Contracts.Options;
using Paperless.Domain.Entities;
using Paperless.Domain.Repositories;

namespace Paperless.Tests.Application.Commands
{
    [TestFixture]
    public class UploadDocumentCommandHandlerTests
    {
        private IDocumentRepository _repo;
        private IObjectStorageService _storage;
        private IMapper _mapper;
        private ILogger<UploadDocumentCommandHandler> _logger;
        private IMediator _mediator;
        private UploadDocumentCommandHandler _handler;
        private MinioOptions _minioOptions;

        [SetUp]
        public void SetUp()
        {
            _repo = Substitute.For<IDocumentRepository>();
            _storage = Substitute.For<IObjectStorageService>();
            _mapper = Substitute.For<IMapper>();
            _logger = Substitute.For<ILogger<UploadDocumentCommandHandler>>();
            _mediator = Substitute.For<IMediator>();

            _minioOptions = new MinioOptions { Bucket = "test-bucket" };
            var optionsWrapper = Substitute.For<IOptions<MinioOptions>>();
            optionsWrapper.Value.Returns(_minioOptions);

            _handler = new UploadDocumentCommandHandler(_repo, _storage, _mapper, _logger, optionsWrapper, _mediator);
        }

        [Test]
        public async Task Handle_ValidRequest_UploadsFileAndSavesToDb()
        {
            // Arrange
            var fileMock = Substitute.For<IFormFile>();
            fileMock.FileName.Returns("test.pdf");
            fileMock.Length.Returns(1024);
            fileMock.ContentType.Returns("application/pdf");
            fileMock.OpenReadStream().Returns(new MemoryStream());

            var command = new UploadDocumentCommand
            {
                Name = "Test Doc",
                File = fileMock,
                Title = "Test Title"
            };

            var docDto = new DocumentDTO { Name = "Test Doc" };
            _mapper.Map<DocumentDTO>(Arg.Any<Document>()).Returns(docDto);

            // Act
            var result = await _handler.Handle(command, CancellationToken.None);

            // Assert
            // verifying DB add
            await _repo.Received(1).AddAsync(Arg.Is<Document>(d => d.Name == "Test Doc"), Arg.Any<CancellationToken>());

            // verify storage upload
            await _storage.Received(1).PutObjectAsync(
                _minioOptions.Bucket,
                Arg.Is<string>(s => s.EndsWith(".pdf")),
                Arg.Any<Stream>(),
                fileMock.Length,
                fileMock.ContentType,
                Arg.Any<CancellationToken>());

            // verifying events published
            await _mediator.Received().Publish(Arg.Any<INotification>(), Arg.Any<CancellationToken>());

            Assert.That(result, Is.EqualTo(docDto));
        }

        [Test]
        public void Handle_StorageFailure_CompensatesByDeletingDbEntry()
        {
            // Arrange
            var fileMock = Substitute.For<IFormFile>();
            fileMock.FileName.Returns("test.pdf");
            // Fix: Set required properties for Document.Create
            fileMock.ContentType.Returns("application/pdf");
            fileMock.Length.Returns(1024);
            fileMock.OpenReadStream().Returns(new MemoryStream());

            var command = new UploadDocumentCommand { Name = "Fail Doc", File = fileMock };

            // simulating storage failure
            _storage.When(x => x.PutObjectAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<Stream>(), Arg.Any<long>(), Arg.Any<string>(), Arg.Any<CancellationToken>()))
                    .Do(x => { throw new Exception("Storage Down"); });

            // Act & Assert
            var ex = Assert.ThrowsAsync<Exception>(async () => await _handler.Handle(command, CancellationToken.None));
            Assert.That(ex.Message, Is.EqualTo("Storage Down"));

            // Verify Compensation: DeleteAsync was called
            _repo.Received(1).DeleteAsync(Arg.Is<Document>(d => d.Name == "Fail Doc"), Arg.Any<CancellationToken>());
        }
    }
}