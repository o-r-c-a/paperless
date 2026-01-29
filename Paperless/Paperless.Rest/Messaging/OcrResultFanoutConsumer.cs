using Microsoft.Extensions.Options;
using Paperless.Contracts.Messages;
using Paperless.Contracts.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Text.Json;

namespace Paperless.Rest.Messaging
{
    public sealed class OcrResultFanoutConsumer : BackgroundService
    {
        private readonly ILogger<OcrResultFanoutConsumer> _logger;
        private readonly RabbitMqOptions _rabbitMqOptions;

        public OcrResultFanoutConsumer
            (
                ILogger<OcrResultFanoutConsumer> logger,
                IOptions<RabbitMqOptions> rabbitMqOptions
            )
        {
            _logger = logger;
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

            IConnection? connection = null;
            IChannel? channel = null;

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    connection = await factory.CreateConnectionAsync(cancellationToken: stoppingToken);
                    channel = await connection.CreateChannelAsync(cancellationToken: stoppingToken);

                    // If we get here, connection is successful
                    _logger.LogInformation("Connected to RabbitMQ.");
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to connect to RabbitMQ. Retrying in 5s...");
                    await Task.Delay(5000, stoppingToken);
                }
            }

            //await using var connection = await factory.CreateConnectionAsync(cancellationToken: stoppingToken);
            //await using var channel = await connection.CreateChannelAsync(cancellationToken: stoppingToken);

            if (stoppingToken.IsCancellationRequested) return;

            try
            {
                // declare queues
                await channel!.QueueDeclareAsync(_rabbitMqOptions.OcrOutQueue, durable: true, exclusive: false, autoDelete: false, arguments: null, cancellationToken: stoppingToken);
                await channel.QueueDeclareAsync(_rabbitMqOptions.GenaiInQueue, durable: true, exclusive: false, autoDelete: false, arguments: null, cancellationToken: stoppingToken);
                await channel.QueueDeclareAsync(_rabbitMqOptions.IndexInQueue, durable: true, exclusive: false, autoDelete: false, arguments: null, cancellationToken: stoppingToken);
                //await channel.QueueDeclareAsync(_genaiInQueue, durable: true, exclusive: false, autoDelete: false, arguments: null, cancellationToken: stoppingToken);
                //await channel.QueueDeclareAsync(_indexInQueue, durable: true, exclusive: false, autoDelete: false, arguments: null, cancellationToken: stoppingToken);

                var consumer = new AsyncEventingBasicConsumer(channel);
                consumer.ReceivedAsync += async (_, ea) =>
                {
                    try
                    {
                        // Validate message shape (so we do not fan-out rubbish)
                        var msg = JsonSerializer.Deserialize<OcrResultMessage>(ea.Body.Span);
                        if (msg == null)
                        {
                            _logger.LogWarning("FanoutConsumer received invalid OCR result message.");
                            return;
                        }

                        _logger.LogInformation(
                            "FanoutConsumer received OCR result for document {Id}. Republishing to {GenAiQueue} and {IndexQueue}.",
                            msg.Id, _rabbitMqOptions.GenaiInQueue, _rabbitMqOptions.IndexInQueue);

                        // publish the original bytes unchanged
                        var props = new BasicProperties { Persistent = true };
                        var body = ea.Body; // ReadOnlyMemory<byte>

                        await channel.BasicPublishAsync(exchange: "", routingKey: _rabbitMqOptions.GenaiInQueue, mandatory: false, basicProperties: props, body: body, cancellationToken: stoppingToken);
                        await channel.BasicPublishAsync(exchange: "", routingKey: _rabbitMqOptions.IndexInQueue, mandatory: false, basicProperties: props, body: body, cancellationToken: stoppingToken);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "FanoutConsumer error while handling OCR result message.");
                    }
                };

                await channel.BasicConsumeAsync(queue: _rabbitMqOptions.OcrOutQueue, autoAck: true, consumer: consumer, cancellationToken: stoppingToken);

                _logger.LogInformation(
                    "OcrResultFanoutConsumer started. Listening on {OcrOut} -> {GenAiIn}, {IndexIn}.",
                    _rabbitMqOptions.OcrOutQueue, _rabbitMqOptions.GenaiInQueue, _rabbitMqOptions.IndexInQueue);

                while (!stoppingToken.IsCancellationRequested)
                {
                    await Task.Delay(500, stoppingToken);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "OcrResultFanoutConsumer encountered a fatal error.");
            }
            finally
            {
                // Cleanup
                if (channel != null) await channel.DisposeAsync();
                if (connection != null) await connection.DisposeAsync();
            }
        }
    }
}
