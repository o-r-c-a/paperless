using Paperless.Domain.Common;
using Paperless.Domain.Events;
using Paperless.Shared.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography.X509Certificates;

namespace Paperless.Domain.Entities
{
    public class Document : Entity
    {
        public Guid Id { get; private set; }        // Unique identifier set in domain not in database
        private string _name = "";
        public string Name 
        { 
            get => _name;
            set
            {
                if(string.IsNullOrWhiteSpace(value)) throw new ArgumentException("Name required.");
                if(value.Length > ValidationRules.NameMaxLength) throw new ArgumentException(ValidationRules.NameTooLongError);
                _name = value;
            }
        }      // Document name
        public DateTime UploadedAt { get; private set; }    // Timestamp of upload
        public string ContentType { get; private set; } = "";// File format (MIME type)
        public long SizeBytes { get; private set; }         // File size in bytes
        private readonly List<Tag> _tags = [];
        public IReadOnlyList<Tag> Tags => _tags.AsReadOnly(); // Tags for categorization
        // Might not apply to all documents
        private string? _title;
        public string? Title 
        { 
            get => _title;
            set
            {
                if(value != null && value.Length > ValidationRules.TitleMaxLength) throw new ArgumentException(ValidationRules.TitleTooLongError);
                _title = value;
            }
        }
        //private List<string>? _authors;
        //public List<string>? Authors => _authors;
        public string? Summary { get; private set; }

        public void SetSummary(string? summary)
        {
            Summary = summary;
        }

        // Constructor is private to enforce use of factory method
        public Document() { }
        public static Document Create
            (string name,
            string contentType,
            long sizeBytes,
            IEnumerable<Tag>? tags = null,
            string? title = null
            //List<string>? authors = null
            )
        {
            // domain-level invariants
            // name
            if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Name required.");
            if (name.Length > ValidationRules.NameMaxLength) throw new ArgumentException(ValidationRules.NameTooLongError);
            // contentType
            if (string.IsNullOrWhiteSpace(contentType)) throw new ArgumentException("Content type required.");
            // sizeBytes
            if (sizeBytes == 0) throw new ArgumentException("Document empty.");
            if (sizeBytes < 0) throw new ArgumentException("Document size invalid.");
            if (sizeBytes > ValidationRules.SizeBytesMaxLength) throw new ArgumentException(ValidationRules.SizeBytesTooLargeError);
            // tags, authors, title
            //if (authors != null && authors.Any(a => a.Length > ValidationRules.AuthorMaxLength)) throw new ArgumentException(ValidationRules.AuthorTooLongError);
            if (title != null && title.Length > ValidationRules.TitleMaxLength) throw new ArgumentException(ValidationRules.TitleTooLongError);

            var doc = new Document
            {
                Id = Guid.NewGuid(),
                Name = name,
                UploadedAt = DateTime.UtcNow,
                ContentType = contentType,
                SizeBytes = sizeBytes,
                //Tags = tags != null ? [.. tags] : [],
                Title = title
                //Authors = authors != null ? [.. authors] : null
            };
            if (tags != null) doc.SetTags(tags);
            //if (authors != null) doc.SetAuthors(authors);
            doc.AddDomainEvent(new DocumentUploadedEvent(doc));
            return doc;
        }
        public void SetTags(IEnumerable<Tag> tags)
        {
            _tags.Clear();
            _tags.AddRange(tags);
        }
        //public void SetAuthors(List<string> authors)
        //{
        //    if (authors.Any(a => a.Length > ValidationRules.AuthorMaxLength)) throw new ArgumentException(ValidationRules.AuthorTooLongError);
        //    _authors ??= [];
        //    _authors.Clear();
        //    _authors.AddRange(authors);
        //}
    }
}
