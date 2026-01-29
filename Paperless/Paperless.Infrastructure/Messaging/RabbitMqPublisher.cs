using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Paperless.Application.Interfaces;
using Paperless.Contracts.Options;
using Paperless.Infrastructure.Exceptions;
using RabbitMQ.Client;
using System.Threading.Channels;

namespace Paperless.Infrastructure.Messaging;
public sealed class RabbitMqPublisher : IRabbitMqPublisher, IDisposable, IAsyncDisposable
{
    private readonly ConnectionFactory _factory;
    private readonly ILogger<RabbitMqPublisher> _logger;

    public RabbitMqPublisher
        (
            IOptions<RabbitMqOptions> opts,
            ILogger<RabbitMqPublisher> logger
        )
    {
        _logger = logger;
        var options = opts.Value;
        _factory = new ConnectionFactory
        {
            HostName = options.Host,
            Port = options.Port,
            UserName = options.Username,
            Password = options.Password
        };
    }

    public async Task PublishAsync(string queueName, ReadOnlyMemory<byte> body, CancellationToken ct = default)
    {
        try
        {
            await using var _connection = await _factory.CreateConnectionAsync(cancellationToken: ct);
            await using var _channel = await _connection.CreateChannelAsync(cancellationToken: ct);

            // Ensure queue exists (idempotent)
            await _channel.QueueDeclareAsync(queue: queueName, durable: true, exclusive: false, autoDelete: false, arguments: null, cancellationToken: ct);

            var props = new BasicProperties { Persistent = true };

            await _channel.BasicPublishAsync(
                exchange: "",
                routingKey: queueName,
                mandatory: false,
                basicProperties: props,
                body: body,
                cancellationToken: ct
            );
            _logger.LogDebug("Published message to {Queue} ({Bytes} bytes)", queueName, body.Length);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to publish message to {Queue}", queueName);
            throw new InfrastructureException($"Failed to publish message to {queueName}", ex);
        }
    }

    public void Dispose() { /* nothing to dispose (we use 'await using' per publish) */ }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

}
