namespace Paperless.Application.Interfaces;

public interface IRabbitMqPublisher
{
    Task PublishAsync(string queueName, ReadOnlyMemory<byte> body, CancellationToken ct = default);
}
