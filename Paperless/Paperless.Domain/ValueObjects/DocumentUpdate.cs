using Paperless.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Paperless.Domain.ValueObjects
{
    public record DocumentUpdate
    {
        public string? Name { get; init; }
        public string? Title { get; init; }
        public IEnumerable<Tag>? Tags { get; init; }
    }
}
