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
    public class DocumentUpdatedEventHandler : INotificationHandler<DocumentUpdatedEvent>
    {
        private readonly IRabbitMqPublisher _publisher;
        private readonly RabbitMqOptions _options;
        private readonly ILogger<DocumentUpdatedEventHandler> _logger;

        public DocumentUpdatedEventHandler(IRabbitMqPublisher publisher, IOptions<RabbitMqOptions> options, ILogger<DocumentUpdatedEventHandler> logger)
        {
            _publisher = publisher;
            _options = options.Value;
            _logger = logger;
        }

        public async Task Handle(DocumentUpdatedEvent notification, CancellationToken ct)
        {
            var doc = notification.Document;
            _logger.LogInformation("Domain Event: Document {Id} updated. Sending update to Index.", doc.Id);

            var msg = new UpdateDocumentIndexMessage
            {
                Id = doc.Id,
                Name = doc.Name,
                Title = doc.Title,
                Tags = doc.Tags.Select(t => t.Name).ToList()
            };

            var bytes = JsonSerializer.SerializeToUtf8Bytes(msg);
            await _publisher.PublishAsync(_options.IndexUpdateQueue, bytes, ct);
        }
    }
}