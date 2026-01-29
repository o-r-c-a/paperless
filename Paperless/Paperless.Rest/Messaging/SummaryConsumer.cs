using Microsoft.Extensions.Options;
using Paperless.Application.Interfaces;
using Paperless.Contracts.Messages;
using Paperless.Contracts.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Text.Json;

namespace Paperless.Rest.Messaging
{
    public sealed class SummaryConsumer : BackgroundService
    {
        private readonly ILogger<SummaryConsumer> _logger;
        private readonly IServiceScopeFactory _scopeFactory; 
        private readonly RabbitMqOptions _rabbitMqOptions;

        public SummaryConsumer
            (
                ILogger<SummaryConsumer> logger,
                IServiceScopeFactory scopeFactory,
                IOptions<RabbitMqOptions> rabbitMqOptions
            )
        {
            _logger = logger;
            _scopeFactory = scopeFactory;
            _rabbitMqOptions = rabbitMqOptions.Value;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            await Task.Delay(2000, stoppingToken); // give RabbitMQ time

            var factory = new ConnectionFactory
            {
                HostName = _rabbitMqOptions.Host,
                Port = _rabbitMqOptions.Port,
                UserName = _rabbitMqOptions.Username,
                Password = _rabbitMqOptions.Password
            };

            await using var connection = await factory.CreateConnectionAsync(cancellationToken: stoppingToken);
            await using var channel = await connection.CreateChannelAsync(cancellationToken: stoppingToken);

            await channel.QueueDeclareAsync(
                queue: _rabbitMqOptions.SummaryInQueue,
                durable: true,
                exclusive: false,
                autoDelete: false,
                arguments: null,
                cancellationToken: stoppingToken);

            var consumer = new AsyncEventingBasicConsumer(channel);
            consumer.ReceivedAsync += async (_, ea) =>
            {
                try
                {
                    var msg = JsonSerializer.Deserialize<SummaryMessage>(ea.Body.Span);
                    if (msg == null)
                    {
                        _logger.LogWarning("Received null or invalid SummaryMessage.");
                        return;
                    }

                    _logger.LogInformation(
                        "Received SummaryMessage for document {Id} ({Name}), length {Length} chars",
                        msg.Id, msg.Name, msg.Summary?.Length ?? 0);

                    if (string.IsNullOrWhiteSpace(msg.Summary))
                    {
                        _logger.LogWarning("Summary is empty for document {Id}, skipping DB update.", msg.Id);
                        return;
                    }

                    using var scope = _scopeFactory.CreateScope();
                    var documentService = scope.ServiceProvider.GetRequiredService<IDocumentService>();

                    await documentService.SetSummaryAsync(msg.Id, msg.Summary, stoppingToken);
                    _logger.LogInformation("Summary stored in database for document {Id}.", msg.Id);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error while handling SummaryMessage.");
                }
            };

            await channel.BasicConsumeAsync(
                queue: _rabbitMqOptions.SummaryInQueue,
                autoAck: true,
                consumer: consumer,
                cancellationToken: stoppingToken);

            _logger.LogInformation("SummaryConsumer started. Listening on {Queue}.", _rabbitMqOptions.SummaryInQueue);

            while (!stoppingToken.IsCancellationRequested)
            {
                await Task.Delay(500, stoppingToken);
            }

            _logger.LogInformation("SummaryConsumer is stopping.");
        }
    }
}
