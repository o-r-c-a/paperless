using Microsoft.Extensions.Options;
using Paperless.Contracts.Messages;
using Paperless.Contracts.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Text;
using System.Text.Json;

namespace Paperless.IndexWorker;

public sealed class IndexWorker : BackgroundService
{
    private readonly RabbitMqOptions _rabbitMqOptions;
    private readonly ElasticSearchOptions _elasticSearchOptions;
    private readonly ILogger<IndexWorker> _logger;
    private readonly HttpClient _http = new();

    public IndexWorker(
        IOptions<RabbitMqOptions> rabbitMqOptions,
        IOptions<ElasticSearchOptions> elasticSearchOptions,
        ILogger<IndexWorker> logger)
    {
        _rabbitMqOptions = rabbitMqOptions.Value;
        _elasticSearchOptions = elasticSearchOptions.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "IndexWorker starting. RabbitMQ={Host}:{Port} Queues=[{In}, {Update}, {Delete}] ES={EsUrl}",
            _rabbitMqOptions.Host, _rabbitMqOptions.Port,
            _rabbitMqOptions.IndexInQueue, _rabbitMqOptions.IndexUpdateQueue, _rabbitMqOptions.IndexDeleteQueue,
            _elasticSearchOptions.Url);

        // startup delay
        await Task.Delay(2000, stoppingToken);

        var factory = new ConnectionFactory
        {
            HostName = _rabbitMqOptions.Host,
            Port = _rabbitMqOptions.Port,
            UserName = _rabbitMqOptions.Username,
            Password = _rabbitMqOptions.Password
        };

        IConnection connection = null!;
        var maxAttempts = _rabbitMqOptions.ConnectionRetries;
        var delay = TimeSpan.FromSeconds(_rabbitMqOptions.ConnectionRetryDelaySeconds);


        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                connection = await factory.CreateConnectionAsync(cancellationToken: stoppingToken);
                _logger.LogInformation("Connected to RabbitMQ on attempt {Attempt}/{Max}.", attempt, maxAttempts);
                break;
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                _logger.LogWarning(ex,
                    "RabbitMQ connection failed (attempt {Attempt}/{Max}). Retrying in {Delay}s...",
                    attempt, maxAttempts, delay.TotalSeconds);
                await Task.Delay(delay, stoppingToken);
            }
        }
        if (connection == null) throw new InvalidOperationException("Could not connect to RabbitMQ.");

        await using var _ = connection;
        var channel = await connection.CreateChannelAsync(cancellationToken: stoppingToken);

        // setup OCR result consumer
        await SetupConsumerAsync<OcrResultMessage>(channel, _rabbitMqOptions.IndexInQueue, async (msg, ct) =>
            await IndexFullAsync(msg, ct), stoppingToken);

        // setup update consumer
        await SetupConsumerAsync<UpdateDocumentIndexMessage>(channel, _rabbitMqOptions.IndexUpdateQueue, async (msg, ct) =>
            await IndexPartialAsync(msg, ct), stoppingToken);

        // setup delete consumer
        await SetupConsumerAsync<DeleteDocumentIndexMessage>(channel, _rabbitMqOptions.IndexDeleteQueue, async (msg, ct) =>
            await DeleteFromIndexAsync(msg, ct), stoppingToken);

        _logger.LogInformation("IndexWorker listening on all queues.");

        while (!stoppingToken.IsCancellationRequested)
            await Task.Delay(1000, stoppingToken);
    }

    private async Task SetupConsumerAsync<T>(IChannel channel, string queueName, Func<T, CancellationToken, Task> handler, CancellationToken ct)
    {
        await channel.QueueDeclareAsync(queue: queueName, durable: true, exclusive: false, autoDelete: false);

        var consumer = new AsyncEventingBasicConsumer(channel);
        consumer.ReceivedAsync += async (_, ea) =>
        {
            try
            {
                var msg = JsonSerializer.Deserialize<T>(ea.Body.Span);
                if (msg != null)
                {
                    await handler(msg, ct);
                    await channel.BasicAckAsync(ea.DeliveryTag, false);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing message from {Queue}", queueName);
                // Optionally Nack
            }
        };

        await channel.BasicConsumeAsync(queue: queueName, autoAck: false, consumer: consumer, cancellationToken: ct);
    }

    private async Task IndexFullAsync(OcrResultMessage msg, CancellationToken ct)
    {
        var url = $"{_elasticSearchOptions.Url.TrimEnd('/')}/{_elasticSearchOptions.IndexName}/_doc/{msg.Id}";
        var payload = new
        {
            id = msg.Id,
            name = msg.Name,
            contentType = msg.ContentType,
            uploadedAt = msg.UploadedAt,
            sizeBytes = msg.SizeBytes,
            text = msg.Text,
            tags = msg.Tags
        };
        await SendToElasticAsync(HttpMethod.Put, url, payload, ct);
        _logger.LogInformation("Indexed document {Id} (Full).", msg.Id);
    }

    private async Task IndexPartialAsync(UpdateDocumentIndexMessage msg, CancellationToken ct)
    {
        // Use _update endpoint for partial updates
        var url = $"{_elasticSearchOptions.Url.TrimEnd('/')}/{_elasticSearchOptions.IndexName}/_update/{msg.Id}";

        // "doc" wrapper is required for partial updates in ES
        var payload = new
        {
            doc = new
            {
                name = msg.Name,
                title = msg.Title,
                tags = msg.Tags
            }
        };

        await SendToElasticAsync(HttpMethod.Post, url, payload, ct);
        _logger.LogInformation("Updated document {Id} (Partial).", msg.Id);
    }

    private async Task DeleteFromIndexAsync(DeleteDocumentIndexMessage msg, CancellationToken ct)
    {
        var url = $"{_elasticSearchOptions.Url.TrimEnd('/')}/{_elasticSearchOptions.IndexName}/_doc/{msg.Id}";
        using var req = new HttpRequestMessage(HttpMethod.Delete, url);
        var res = await _http.SendAsync(req, ct);

        if (!res.IsSuccessStatusCode && res.StatusCode != System.Net.HttpStatusCode.NotFound)
        {
            var body = await res.Content.ReadAsStringAsync(ct);
            _logger.LogError("Failed to delete document {Id} from Elastic: {Status} {Body}", msg.Id, res.StatusCode, body);
        }
        else
        {
            _logger.LogInformation("Deleted document {Id} from Elastic.", msg.Id);
        }
    }

    private async Task SendToElasticAsync(HttpMethod method, string url, object payload, CancellationToken ct)
    {
        var json = JsonSerializer.Serialize(payload);
        using var req = new HttpRequestMessage(method, url)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };

        var res = await _http.SendAsync(req, ct);
        if (!res.IsSuccessStatusCode)
        {
            var body = await res.Content.ReadAsStringAsync(ct);
            throw new InvalidOperationException($"Elasticsearch operation failed: {res.StatusCode} {body}");
        }
    }
}