using FluentValidation.TestHelper;
using Microsoft.AspNetCore.Http;
using NSubstitute;
using NUnit.Framework;
using Paperless.Rest.Models;
using Paperless.Rest.Validators;
using Paperless.Shared.Utils;
using System.IO;

namespace Paperless.Tests.Rest.Validators
{
    [TestFixture]
    public class CreateDocumentRequestValidatorTests
    {
        private CreateDocumentRequestValidator _validator;
        private IFormFile _validFileMock;

        [SetUp]
        public void SetUp()
        {
            _validator = new CreateDocumentRequestValidator();

            // creating a file mock to use as a base for all tests
            // to prevents NullReferenceExceptions on property access
            _validFileMock = Substitute.For<IFormFile>();
            _validFileMock.FileName.Returns("test.pdf");
            _validFileMock.Length.Returns(1024);
            _validFileMock.ContentType.Returns("application/pdf");
        }

        [Test]
        public void Should_Have_Error_When_Name_Is_Empty()
        {
            // Arrange
            var model = new CreateDocumentRequest
            {
                Name = "",
                File = _validFileMock // Must be present to avoid exception on File.ContentType rules
            };

            // Act
            var result = _validator.TestValidate(model);

            // Assert
            result.ShouldHaveValidationErrorFor(x => x.Name);
        }

        [Test]
        public void Should_Have_Error_When_File_Has_Invalid_Extension()
        {
            var file = Substitute.For<IFormFile>();
            file.FileName.Returns("malicious.exe");
            file.Length.Returns(100);
            file.ContentType.Returns("application/octet-stream");

            var model = new CreateDocumentRequest { Name = "Valid Name", File = file };
            var result = _validator.TestValidate(model);

            result.ShouldHaveValidationErrorFor(x => x.File.FileName);
        }

        [Test]
        public void Should_Have_Error_When_File_Is_Too_Large()
        {
            var file = Substitute.For<IFormFile>();
            file.FileName.Returns("big.pdf");
            file.Length.Returns(ValidationRules.SizeBytesMaxLength + 1);
            file.ContentType.Returns("application/pdf");

            var model = new CreateDocumentRequest { File = file };
            var result = _validator.TestValidate(model);

            result.ShouldHaveValidationErrorFor(x => x.File.Length);
        }

        [Test]
        public void Should_Pass_When_Request_Is_Valid()
        {

            var model = new CreateDocumentRequest
            {
                Name = "Valid Name",
                Title = "Valid Title",
                File = _validFileMock
            };

            var result = _validator.TestValidate(model);
            result.ShouldNotHaveAnyValidationErrors();
        }
    }
}