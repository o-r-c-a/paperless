using AutoMapper;
using FluentValidation;
using FluentValidation.Results;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Paperless.Application.Commands;
using Paperless.Application.DTOs;
using Paperless.Application.Interfaces;
using Paperless.Domain.Exceptions;
using Paperless.Rest.Controllers;
using Paperless.Rest.Models;

namespace Paperless.Tests.Rest.Controllers
{
    [TestFixture]
    public class DocumentsControllerTests
    {
        private IMediator _mediator;
        private IDocumentService _documentService;
        private ILogger<DocumentsController> _logger;
        private IMapper _mapper;
        private IValidator<CreateDocumentRequest> _createDocValidator;
        private IValidator<UpdateDocumentRequest> _updateDocValidator;
        private DocumentsController _controller;

        [SetUp]
        public void Setup()
        {
            _mediator = Substitute.For<IMediator>();
            _documentService = Substitute.For<IDocumentService>();
            _logger = Substitute.For<ILogger<DocumentsController>>();
            _mapper = Substitute.For<IMapper>();
            _createDocValidator = Substitute.For<IValidator<CreateDocumentRequest>>();
            _updateDocValidator = Substitute.For<IValidator<UpdateDocumentRequest>>();

            _controller = new DocumentsController(
                _mediator,
                _documentService,
                _logger,
                _mapper,
                _createDocValidator,
                _updateDocValidator
            );
        }

        #region Create Tests

        [Test]
        public async Task Create_WithValidData_ReturnsCreatedAtAction()
        {
            // Arrange
            var request = CreateMockCreateDocumentRequest("Test Doc", "test.pdf");
            var command = new UploadDocumentCommand { Name = "Test Doc" };
            var responseDto = new DocumentDTO { Id = Guid.NewGuid(), Name = "Test Doc" };
            var responseModel = new DocumentResponse { Id = responseDto.Id, Name = "Test Doc" };

            // Mock Validation
            _createDocValidator.ValidateAsync(request, Arg.Any<CancellationToken>())
                .Returns(new ValidationResult());

            // Mock Mapping
            _mapper.Map<UploadDocumentCommand>(request).Returns(command);

            // Mock Mediator
            _mediator.Send(command, Arg.Any<CancellationToken>()).Returns(responseDto);

            // Mock Response Mapping
            _mapper.Map<DocumentResponse>(responseDto).Returns(responseModel);

            // Act
            var result = await _controller.Create(request, CancellationToken.None);

            // Assert
            Assert.That(result, Is.InstanceOf<CreatedAtActionResult>());
            var createdResult = (CreatedAtActionResult)result;
            Assert.That(createdResult.ActionName, Is.EqualTo(nameof(DocumentsController.GetById)));
            Assert.That(createdResult.RouteValues!["id"], Is.EqualTo(responseModel.Id));
            Assert.That(createdResult.Value, Is.EqualTo(responseModel));
        }

        [Test]
        public async Task Create_WithInvalidData_ReturnsBadRequest()
        {
            // Arrange
            var request = CreateMockCreateDocumentRequest("", "test.pdf");
            var validationFailures = new List<ValidationFailure> { new("Name", "Required") };

            _createDocValidator.ValidateAsync(request, Arg.Any<CancellationToken>())
                .Returns(new ValidationResult(validationFailures));

            // Act
            var result = await _controller.Create(request, CancellationToken.None);

            // Assert
            Assert.That(result, Is.InstanceOf<BadRequestObjectResult>());
            await _mediator.DidNotReceive().Send(Arg.Any<UploadDocumentCommand>(), Arg.Any<CancellationToken>());
        }

        #endregion

        #region GetById Tests

        [Test]
        public async Task GetById_WithExistingId_ReturnsOk()
        {
            // Arrange
            var id = Guid.NewGuid();
            var dto = new DocumentDTO { Id = id, Name = "Doc" };
            var response = new DocumentResponse { Id = id, Name = "Doc" };

            _documentService.GetDocumentByIdAsync(id, Arg.Any<CancellationToken>()).Returns(dto);
            _mapper.Map<DocumentResponse>(dto).Returns(response);

            // Act
            var result = await _controller.GetById(id, CancellationToken.None);

            // Assert
            Assert.That(result, Is.InstanceOf<OkObjectResult>());
            Assert.That(((OkObjectResult)result).Value, Is.EqualTo(response));
        }

        [Test]
        public void GetById_WhenServiceThrows_PropagatesException()
        {
            // Arrange
            var id = Guid.NewGuid();
            _documentService.GetDocumentByIdAsync(id, Arg.Any<CancellationToken>())
                .ThrowsAsync(new DocumentDoesNotExistException(id));

            // Act & Assert
            Assert.ThrowsAsync<DocumentDoesNotExistException>(async () =>
                await _controller.GetById(id, CancellationToken.None));
        }

        #endregion

        #region Update Tests

        [Test]
        public async Task Update_WithValidData_ReturnsNoContent()
        {
            // Arrange
            var id = Guid.NewGuid();
            var request = new UpdateDocumentRequest { Name = "New Name" };

            _updateDocValidator.ValidateAsync(request, Arg.Any<CancellationToken>())
                .Returns(new ValidationResult());

            // Act
            var result = await _controller.Update(id, request, CancellationToken.None);

            // Assert
            Assert.That(result, Is.InstanceOf<NoContentResult>());
            await _mediator.Received(1).Send(
                Arg.Is<UpdateDocumentCommand>(c => c.Id == id && c.Name == "New Name"),
                Arg.Any<CancellationToken>());
        }

        [Test]
        public async Task Update_WithInvalidData_ReturnsBadRequest()
        {
            // Arrange
            var id = Guid.NewGuid();
            var request = new UpdateDocumentRequest();
            var validationFailures = new List<ValidationFailure> { new("Name", "Error") };

            _updateDocValidator.ValidateAsync(request, Arg.Any<CancellationToken>())
                .Returns(new ValidationResult(validationFailures));

            // Act
            var result = await _controller.Update(id, request, CancellationToken.None);

            // Assert
            Assert.That(result, Is.InstanceOf<BadRequestObjectResult>());
            await _mediator.DidNotReceive().Send(Arg.Any<UpdateDocumentCommand>(), Arg.Any<CancellationToken>());
        }

        #endregion

        #region Delete Tests

        [Test]
        public async Task Delete_ReturnsNoContent()
        {
            // Arrange
            var id = Guid.NewGuid();

            // Act
            var result = await _controller.Delete(id, CancellationToken.None);

            // Assert
            Assert.That(result, Is.InstanceOf<NoContentResult>());
            await _mediator.Received(1).Send(
                Arg.Is<DeleteDocumentCommand>(c => c.Id == id),
                Arg.Any<CancellationToken>());
        }

        #endregion

        #region List Tests

        [Test]
        public async Task List_ReturnsOkWithDocuments()
        {
            // Arrange
            var dtos = new List<DocumentDTO> { new(), new() };
            var responses = new List<DocumentResponse> { new(), new() };

            _documentService.ListDocumentsAsync(0, 50, Arg.Any<CancellationToken>()).Returns(dtos);
            _mapper.Map<IReadOnlyList<DocumentResponse>>(dtos).Returns(responses);

            // Act
            var result = await _controller.List(0, 50, CancellationToken.None);

            // Assert
            Assert.That(result, Is.InstanceOf<OkObjectResult>());
            Assert.That(((OkObjectResult)result).Value, Is.EqualTo(responses));
        }

        #endregion

        #region Helpers

        private CreateDocumentRequest CreateMockCreateDocumentRequest(string name, string fileName)
        {
            var fileMock = Substitute.For<IFormFile>();
            fileMock.FileName.Returns(fileName);
            fileMock.Length.Returns(1024);
            fileMock.OpenReadStream().Returns(new MemoryStream());

            return new CreateDocumentRequest
            {
                Name = name,
                File = fileMock
            };
        }

        #endregion
    }
}