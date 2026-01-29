using AutoMapper;
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
    public class DocumentUploadedEventHandler : INotificationHandler<DocumentUploadedEvent>
    {
        private readonly IRabbitMqPublisher _publisher;
        private readonly RabbitMqOptions _mqOptions;
        private readonly ILogger<DocumentUploadedEventHandler> _logger;
        private readonly IMapper _mapper;

        public DocumentUploadedEventHandler(
            IRabbitMqPublisher publisher,
            IOptions<RabbitMqOptions> mqOptions,
            ILogger<DocumentUploadedEventHandler> logger,
            IMapper mapper)
        {
            _publisher = publisher;
            _mqOptions = mqOptions.Value;
            _logger = logger;
            _mapper = mapper;
        }

        public async Task Handle(DocumentUploadedEvent notification, CancellationToken ct)
        {
            var doc = notification.Document;
            _logger.LogInformation("Domain Event Received: Document {DocumentId} uploaded. Preparing OCR job.", doc.Id);

            var msg = _mapper.Map<OcrJobMessage>(doc);
            var bytes = JsonSerializer.SerializeToUtf8Bytes(msg);

            try
            {
                await _publisher.PublishAsync(_mqOptions.OcrInQueue, bytes, ct);
                _logger.LogInformation("Published OcrJobMessage for {DocumentId} to queue {Queue}", doc.Id, _mqOptions.OcrInQueue);
            }
            catch (Exception pubEx)
            {
                // Do not break the request if publishing fails; log and continue.
                _logger.LogError(pubEx, "Failed to publish message for document {DocumentId}", doc.Id);
            }
        }
    }
}