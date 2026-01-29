using NUnit.Framework;
using Paperless.BatchAccess;
using System;
using System.IO;

namespace Paperless.Tests.BatchAccess
{
    [TestFixture]
    public class AccessLogXmlParserTests
    {
        private AccessLogXmlParser _parser;
        private string _tempFile;

        [SetUp]
        public void SetUp()
        {
            _parser = new AccessLogXmlParser();
            _tempFile = Path.GetTempFileName();
        }

        [TearDown]
        public void TearDown()
        {
            if (File.Exists(_tempFile)) File.Delete(_tempFile);
        }

        [Test]
        public void ParseAndAggregate_ValidXml_ReturnsAggregatedCounts()
        {
            // Arrange
            var docId = Guid.NewGuid();
            var xml = $@"
                <accessStatistics>
                    <event documentId='{docId}' type='upload' at='2026-01-10T10:00:00Z' />
                    <event documentId='{docId}' type='download' at='2026-01-10T11:00:00Z' />
                    <event documentId='{docId}' type='download' at='2026-01-10T12:00:00Z' />
                </accessStatistics>";
            File.WriteAllText(_tempFile, xml);

            // Act
            var result = _parser.ParseAndAggregate(_tempFile);

            // Assert
            var dateKey = new DateTime(2026, 1, 10, 0, 0, 0, DateTimeKind.Utc);

            // Should have 1 upload
            Assert.That(result[(docId, dateKey, "upload")], Is.EqualTo(1));
            // Should have 2 downloads aggregated
            Assert.That(result[(docId, dateKey, "download")], Is.EqualTo(2));
        }

        [Test]
        public void ParseAndAggregate_InvalidXmlStructure_ThrowsFormatException()
        {
            File.WriteAllText(_tempFile, "<invalidRoot></invalidRoot>");
            Assert.Throws<FormatException>(() => _parser.ParseAndAggregate(_tempFile));
        }

        [Test]
        public void ParseAndAggregate_InvalidDateFormat_ThrowsFormatException()
        {
            var xml = @"
                <accessStatistics>
                    <event documentId='GUID' type='upload' at='NOT-A-DATE' />
                </accessStatistics>";
            File.WriteAllText(_tempFile, xml);

            Assert.Throws<FormatException>(() => _parser.ParseAndAggregate(_tempFile));
        }

        [Test]
        public void ParseAndAggregate_UnknownAccessType_ThrowsFormatException()
        {
            var xml = $@"
                <accessStatistics>
                    <event documentId='{Guid.NewGuid()}' type='hacked' at='2026-01-10T10:00:00Z' />
                </accessStatistics>";
            File.WriteAllText(_tempFile, xml);

            var ex = Assert.Throws<FormatException>(() => _parser.ParseAndAggregate(_tempFile));
            Assert.That(ex.Message, Does.Contain("Unsupported access type"));
        }
    }
}