using NUnit.Framework;
using Paperless.Domain.Entities;
using Paperless.Shared.Utils;
using System;
using System.Linq;

namespace Paperless.Tests.Domain.Entities
{
    [TestFixture]
    public class DocumentTests
    {
        [Test]
        public void Create_WithValidData_ReturnsDocument()
        {
            var doc = Document.Create("valid.pdf", "application/pdf", 1024);

            Assert.That(doc.Id, Is.Not.EqualTo(Guid.Empty));
            Assert.That(doc.Name, Is.EqualTo("valid.pdf"));
            Assert.That(doc.UploadedAt, Is.EqualTo(DateTime.UtcNow).Within(1).Seconds);
        }

        [Test]
        public void Create_Throws_WhenNameIsEmpty()
        {
            var ex = Assert.Throws<ArgumentException>(() =>
                Document.Create("", "application/pdf", 1024));
            Assert.That(ex.Message, Does.Contain("Name required"));
        }

        [Test]
        public void Create_Throws_WhenFileIsEmpty()
        {
            var ex = Assert.Throws<ArgumentException>(() =>
                Document.Create("test.pdf", "application/pdf", 0));
            Assert.That(ex.Message, Does.Contain("Document empty"));
        }

        [Test]
        public void Create_Throws_WhenSizeIsTooLarge()
        {
            var ex = Assert.Throws<ArgumentException>(() =>
                Document.Create("test.pdf", "application/pdf", ValidationRules.SizeBytesMaxLength + 1));
            Assert.That(ex.Message, Does.Contain("size must be less than"));
        }

        [Test]
        public void Create_AssignsTags_WhenProvided()
        {
            var tags = new[] { new Tag { Name = "invoice" }, new Tag { Name = "paid" } };
            var doc = Document.Create("doc.pdf", "application/pdf", 100, tags);

            Assert.That(doc.Tags, Has.Count.EqualTo(2));
            Assert.That(doc.Tags.Any(t => t.Name == "invoice"), Is.True);
        }
    }
}