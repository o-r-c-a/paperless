using MediatR;
using Paperless.Application.DTOs;
using System.Text.Json.Serialization;

namespace Paperless.Application.Commands
{
    // returns void or Unit as usually NoContent is returned on update
    public class UpdateDocumentCommand : IRequest
    {
        [JsonIgnore] // Id comes from route in Controller
        public Guid Id { get; set; }
        public string? Name { get; set; }
        public IEnumerable<string>? Tags { get; set; }
        public string? Title { get; set; }
    }
}