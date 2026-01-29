using AutoMapper;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Paperless.Application.Commands;
using Paperless.Rest.Mapper;
using Paperless.Rest.Models;

namespace Paperless.Tests.Mappings
{
    [TestFixture]
    public class MappingTests
    {
        private IMapper _mapper;

        [SetUp]
        public void Setup()
        {
            var config = new MapperConfiguration(cfg =>
            {
                cfg.AddProfile(new RequestDocumentProfile());
            }, NullLoggerFactory.Instance);
            _mapper = config.CreateMapper();
        }

        [Test]
        public void Map_CreateDocumentRequest_To_Command_NormalizesTags()
        {
            // Arrange
            var request = new CreateDocumentRequest
            {
                Name = "Doc",
                // Mixed case, whitespace, duplicates
                Tags = new[] { "Invoice", " invoice ", "Important" }
            };

            // Act
            var command = _mapper.Map<UploadDocumentCommand>(request);

            // Assert
            Assert.That(command.Tags, Is.Not.Null);
            var tags = command.Tags.ToList();

            // Should be 2 distinct tags: "invoice" and "important"
            Assert.That(tags, Has.Count.EqualTo(2));
            Assert.That(tags.Any(t => t.Name == "invoice"), Is.True);
            Assert.That(tags.Any(t => t.Name == "important"), Is.True);
        }

        [Test]
        public void Map_CreateDocumentRequest_WithNullTags_ReturnsNullOrEmpty()
        {
            var request = new CreateDocumentRequest { Name = "Doc", Tags = null };
            var command = _mapper.Map<UploadDocumentCommand>(request);

            Assert.That(command.Tags, Is.Null);
        }
    }
}