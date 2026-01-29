using MediatR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Paperless.Application.Interfaces;
using Paperless.Contracts.Messages;
using Paperless.Contracts.Options;
using Paperless.Domain.Events;
using System.Text.Json;

namespace Paperless.Application.EventHandlers
{
    public class DocumentDeletedEventHandler : INotificationHandler<DocumentDeletedEvent>
    {
        private readonly IRabbitMqPublisher _publisher;
        private readonly RabbitMqOptions _options;
        private readonly ILogger<DocumentDeletedEventHandler> _logger;

        public DocumentDeletedEventHandler(IRabbitMqPublisher publisher, IOptions<RabbitMqOptions> options, ILogger<DocumentDeletedEventHandler> logger)
        {
            _publisher = publisher;
            _options = options.Value;
            _logger = logger;
        }

        public async Task Handle(DocumentDeletedEvent notification, CancellationToken ct)
        {
            _logger.LogInformation("Domain Event: Document {Id} deleted. Sending delete to Index.", notification.Id);

            var msg = new DeleteDocumentIndexMessage { Id = notification.Id };
            var bytes = JsonSerializer.SerializeToUtf8Bytes(msg);

            await _publisher.PublishAsync(_options.IndexDeleteQueue, bytes, ct);
        }
    }
}