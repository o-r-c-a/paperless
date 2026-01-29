using MediatR;
using Microsoft.AspNetCore.Mvc;
using Paperless.Application.Queries;

namespace Paperless.Rest.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public sealed class SearchController : ControllerBase
    {
        private readonly IMediator _mediator;
        private readonly ILogger<SearchController> _logger;

        public SearchController(IMediator mediator, ILogger<SearchController> logger)
        {
            _mediator = mediator;
            _logger = logger;
        }

        [HttpGet]
        public async Task<IActionResult> Search([FromQuery] string query, CancellationToken ct)
        {
            // The controller now delegates all business logic to the Mediator.
            var results = await _mediator.Send(new SearchDocumentsQuery(query), ct);
            return Ok(results);
        }
    }
}
