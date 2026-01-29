using MediatR;
using Microsoft.AspNetCore.Http;
using Paperless.Application.DTOs;
using Paperless.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Paperless.Application.Commands
{
    // Request returns a DocumentDTO
    public class UploadDocumentCommand : IRequest<DocumentDTO>
    {
        public string Name { get; set; } = "";
        public IFormFile File { get; set; } = null!;
        public string? Title { get; set; }
        public IEnumerable<Tag> Tags { get; set; } = [];
    }
}
