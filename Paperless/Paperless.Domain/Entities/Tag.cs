using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Paperless.Shared.Utils;

namespace Paperless.Domain.Entities
{
    public class Tag
    {
        private string _name = "";
        [Key]
        public string Name
        {
            get => _name;
            set
            {
                if (string.IsNullOrWhiteSpace(value)) throw new ArgumentException("Tag name required.");
                if (value.Length < ValidationRules.TagMinLength) throw new ArgumentException(ValidationRules.TagTooShortError);
                if (value.Length > ValidationRules.TagMaxLength) throw new ArgumentException(ValidationRules.TagTooLongError);
                _name = value.ToLower().Trim();
            }
        }
        public ICollection<Document> Documents { get; set; } = [];
    }
}
