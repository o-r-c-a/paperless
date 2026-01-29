using FluentValidation;
using Paperless.Domain.Exceptions;
using Paperless.Infrastructure.Exceptions;
using System.Text.Json;

namespace Paperless.Rest.Middleware
{
    public class ApiExceptionMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<ApiExceptionMiddleware> _logger;
        private readonly JsonSerializerOptions _jsonOptions;

        public ApiExceptionMiddleware(RequestDelegate next, ILogger<ApiExceptionMiddleware> logger)
        {
            _next = next ?? throw new ArgumentNullException(nameof(next));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _jsonOptions = new()
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                WriteIndented = true
            };
        }

        public async Task InvokeAsync(HttpContext context)
        {
            try
            {
                // forward to the next middleware / endpoint in the pipeline
                await _next(context);
            }
            catch (Exception ex)
            {
                // log, then translate into HTTP response
                _logger.LogError(ex, "Unhandled exception while processing request");
                await HandleExceptionAsync(context, ex);
            }
        }

        private Task HandleExceptionAsync(HttpContext ctx, Exception ex)
        {
            (int statusCode, object payload) = ex switch
            {
                //BusinessValidationException vex => (StatusCodes.Status400BadRequest, new { error = vex.Message }),
                DocumentAlreadyExistsException dex => (StatusCodes.Status409Conflict, (object)new { error = dex.Message}),
                DocumentDoesNotExistException dnex => (StatusCodes.Status404NotFound, (object)new { error = dnex.Message }),
                SearchIndexMissingException siex => (StatusCodes.Status404NotFound, (object)new
                {
                    error = siex.Message,
                    index = siex.IndexName
                }),
                ValidationException vex => (StatusCodes.Status400BadRequest, (object)new
                {
                    error = "Validation Failed",
                    errors = vex.Errors.Select(e => new
                    {
                        Field = e.PropertyName,
                        Message = e.ErrorMessage
                    })
                }),
                _ => (StatusCodes.Status500InternalServerError, (object)new { error = "An unexpected error occurred." })
            };

            ctx.Response.ContentType = "application/json";
            ctx.Response.StatusCode = statusCode;

            var json = JsonSerializer.Serialize(payload, _jsonOptions);

            return ctx.Response.WriteAsync(json);
            }
        }
    }