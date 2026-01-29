using AutoMapper;
using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Paperless.Application.Commands;
using Paperless.Application.Interfaces;
using Paperless.Rest.Models;

namespace Paperless.Rest.Controllers
{
    [ApiController]
    [Route("/api/[controller]")]
    public class DocumentsController : ControllerBase
    {
        private readonly IMediator _mediator;
        // keeping IDocumentService for Get / List operations
        private readonly IDocumentService _documentService;
        private readonly ILogger<DocumentsController> _logger;
        private readonly IMapper _mapper;
        private readonly IValidator<CreateDocumentRequest> _createDocValidator;
        private readonly IValidator<UpdateDocumentRequest> _updateDocValidator;
        public DocumentsController
            (
            IMediator mediator,
            IDocumentService documentService,
            ILogger<DocumentsController> logger,
            IMapper mapper,
            IValidator<CreateDocumentRequest> createDocValidator,
            IValidator<UpdateDocumentRequest> updateDocValidator
            )
        {
            _mediator = mediator;
            _documentService = documentService;
            _logger = logger;
            _mapper = mapper;
            _createDocValidator = createDocValidator;
            _updateDocValidator = updateDocValidator;
        }

        // POST api/documents
        [HttpPost]
        [ProducesResponseType(StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> Create([FromForm] CreateDocumentRequest docRequest, CancellationToken ct)
        {
            _logger.LogInformation("Given Name: {DocumentName}, File name: {FileFileName}, Size: {FileSize} bytes", docRequest.Name, docRequest.File.FileName, docRequest.File.Length);
            
            var validationResult = await _createDocValidator.ValidateAsync(docRequest, ct);
            if (!validationResult.IsValid)
                return BadRequest(validationResult.ToString());

            var command = _mapper.Map<UploadDocumentCommand>(docRequest);
            
            // dispatch command to handler
            var responseDto = await _mediator.Send(command, ct);
            var responseDoc = _mapper.Map<DocumentResponse>(responseDto);

            return CreatedAtAction(nameof(GetById), new { id = responseDoc.Id }, responseDoc);
        }

        // GET api/documents/{id}
        [HttpGet("{id:guid}")]
        public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
        {
            _logger.LogInformation("Retrieving document with ID: {DocumentId}", id);
            var docDto = await _documentService.GetDocumentByIdAsync(id, ct);
            var response = _mapper.Map<DocumentResponse>(docDto);
            return Ok(response);
        }

        // PUT api/documents/{id}
        [HttpPut("{id:guid}")]
        public async Task<IActionResult> Update(Guid id, [FromBody] UpdateDocumentRequest docRequest, CancellationToken ct)
        {
            _logger.LogInformation("Updating document with ID: {DocumentId}", id);
            var validationResult = await _updateDocValidator.ValidateAsync(docRequest, ct);
            if (!validationResult.IsValid)
                return BadRequest(validationResult.ToString());

            _logger.LogDebug("request tags: {Tags}", docRequest.Tags is null ? "null" : string.Join(", ", docRequest.Tags));

            var command = new UpdateDocumentCommand
            {
                Id = id,
                Name = docRequest.Name,
                Title = docRequest.Title,
                Tags = docRequest.Tags
            };

            await _mediator.Send(command, ct);
            return NoContent();
        }

        // DELETE api/documents/{id}
        [HttpDelete("{id:guid}")]
        public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
        {
            _logger.LogInformation("Deleting document with ID: {DocumentId}", id);
            await _mediator.Send(new DeleteDocumentCommand(id), ct);
            return NoContent();
        }

        // Optional list endpoint to test repository ListAsync
        // GET api/documents?skip=0&take=50
        [HttpGet]
        public async Task<IActionResult> List([FromQuery] int skip = 0, [FromQuery] int take = 50, CancellationToken ct = default)
        {
            _logger.LogInformation("Listing documents with skip: {Skip}, take: {Take}", skip, take);
            var docDTOs = await _documentService.ListDocumentsAsync(skip, take, ct);
            var docResponses = _mapper.Map<IReadOnlyList<DocumentResponse>>(docDTOs);
            return Ok(docResponses);
        }
    }
}
