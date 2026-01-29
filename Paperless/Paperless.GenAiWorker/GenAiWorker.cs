using Microsoft.Extensions.Options;
using Paperless.Contracts.Messages;
using Paperless.Contracts.Options;
using Paperless.Infrastructure.Messaging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Text;
using System.Text.Json;

namespace Paperless.GenAiWorker
{
    public sealed class GenAiWorker : BackgroundService
    {
        private readonly RabbitMqOptions _rabbitMqOptions;
        private readonly GeminiOptions _geminiOptions;
        //private readonly 
        private readonly ILogger<GenAiWorker> _logger;
        private readonly HttpClient _httpClient = new();

        public GenAiWorker(
            IOptions<RabbitMqOptions> rabbitMqOptions,
            IOptions<GeminiOptions> geminiOptions,
            ILogger<GenAiWorker> logger)
        {
            _rabbitMqOptions = rabbitMqOptions.Value;
            _geminiOptions = geminiOptions.Value;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation(
                "GenAiWorker starting. Host={Host}:{Port}, User={User}, In={QueueIn}, Out={QueueOut}",
                _rabbitMqOptions.Host, _rabbitMqOptions.Port, _rabbitMqOptions.Username, _rabbitMqOptions.GenaiInQueue, _rabbitMqOptions.SummaryInQueue);

            if (string.IsNullOrWhiteSpace(_geminiOptions.ApiKey))
            {
                _logger.LogError("GEMINI_API_KEY is not configured. GenAiWorker will not be able to call Gemini.");
            }

            await Task.Delay(2000, stoppingToken); // give RabbitMQ some time to start

            var factory = new ConnectionFactory
            {
                HostName = _rabbitMqOptions.Host,
                Port = _rabbitMqOptions.Port,
                UserName = _rabbitMqOptions.Username,
                Password = _rabbitMqOptions.Password
            };

            await using var connection = await factory.CreateConnectionAsync(cancellationToken: stoppingToken);
            await using var channel = await connection.CreateChannelAsync(cancellationToken: stoppingToken);

            // Declare input/output queues
            await channel.QueueDeclareAsync(
                queue: _rabbitMqOptions.GenaiInQueue,
                durable: true,
                exclusive: false,
                autoDelete: false,
                arguments: null,
                cancellationToken: stoppingToken);

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
                    var msg = JsonSerializer.Deserialize<OcrResultMessage>(ea.Body.Span);
                    if (msg == null)
                    {
                        _logger.LogWarning("Received null or invalid OCR result message.");
                        return;
                    }

                    _logger.LogInformation(
                        "Received OCR result for document {Id} ({Name}), text length {Length}",
                        msg.Id, msg.Name, msg.Text?.Length ?? 0);

                    if (string.IsNullOrWhiteSpace(msg.Text))
                    {
                        _logger.LogWarning("OCR text is empty for document {Id}, skipping Gemini call.", msg.Id);
                        return;
                    }

                    var summaryText = await GenerateSummaryWithRetryAsync(msg.Text, stoppingToken);

                    if (string.IsNullOrWhiteSpace(summaryText))
                    {
                        _logger.LogWarning("Gemini returned empty summary for document {Id}.", msg.Id);
                        return;
                    }

                    var summaryMessage = new SummaryMessage
                    {
                        Id = msg.Id,
                        Name = msg.Name,
                        Summary = summaryText
                    };

                    var body = JsonSerializer.SerializeToUtf8Bytes(summaryMessage);
                    var props = new BasicProperties { Persistent = true };

                    await channel.BasicPublishAsync(
                        exchange: "",
                        routingKey: _rabbitMqOptions.SummaryInQueue,
                        mandatory: false,
                        basicProperties: props,
                        body: body,
                        cancellationToken: stoppingToken);

                    _logger.LogInformation(
                        "Published summary for document {Id} to queue {Queue} (len: {Length} chars)",
                        msg.Id, _rabbitMqOptions.SummaryInQueue, summaryText.Length);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error while handling OCR result message.");
                }
            };

            await channel.BasicConsumeAsync(
                queue: _rabbitMqOptions.GenaiInQueue,
                autoAck: true,
                consumer: consumer,
                cancellationToken: stoppingToken);

            _logger.LogInformation(
                "GenAiWorker started. Listening on {QueueIn} and publishing to {QueueOut}.",
                _rabbitMqOptions.GenaiInQueue, _rabbitMqOptions.SummaryInQueue);

            // Keep process alive
            while (!stoppingToken.IsCancellationRequested)
            {
                await Task.Delay(500, stoppingToken);
            }

            _logger.LogInformation("GenAiWorker is stopping.");
        }

        private async Task<string> GenerateSummaryWithRetryAsync(string text, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(_geminiOptions.ApiKey))
            {
                _logger.LogWarning("Cannot call Gemini because GEMINI_API_KEY is missing.");
                return string.Empty;
            }

            for (int attempt = 1; attempt <= _geminiOptions.MaxRetries; attempt++)
            {
                try
                {
                    return await GenerateSummaryAsync(text, ct);
                }
                catch (Exception ex)
                {
                    if (attempt == _geminiOptions.MaxRetries)
                    {
                        _logger.LogError(ex, "Gemini API failed on final attempt {Attempt}.", attempt);
                        return string.Empty;
                    }

                    _logger.LogWarning(ex, "Gemini API failed (Attempt {Attempt}/{Max}). Retrying in {Delay}s...", attempt, _geminiOptions.MaxRetries, _geminiOptions.RetryDelayMilliseconds);
                    await Task.Delay(_geminiOptions.RetryDelayMilliseconds, ct);
                }
            }
            return string.Empty;
        }

        private async Task<string> GenerateSummaryAsync(string text, CancellationToken ct)
        {
            var endpoint = $"https://generativelanguage.googleapis.com/v1beta/models/{_geminiOptions.Model}:generateContent";

            using var request = new HttpRequestMessage(HttpMethod.Post, endpoint);
            request.Headers.Add("x-goog-api-key", _geminiOptions.ApiKey);

            // Small prompt around the OCR text
            var prompt = $"Summarize the following document in 5-7 concise sentences:\n\n{text}";

            var payload = new
            {
                contents = new[]
                {
                    new
                    {
                        parts = new[]
                        {
                            new { text = prompt }
                        }
                    }
                }
            };

            request.Content = new StringContent(
                JsonSerializer.Serialize(payload),
                Encoding.UTF8,
                "application/json");

            var response = await _httpClient.SendAsync(request, ct);

            // throw if not successful, to trigger retry logic
            response.EnsureSuccessStatusCode();

            using var stream = await response.Content.ReadAsStreamAsync(ct);
            using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct);

            // Response structure: candidates[0].content.parts[0].text
            var root = doc.RootElement;

            if (!root.TryGetProperty("candidates", out var candidates) ||
                candidates.GetArrayLength() == 0)
            {
                _logger.LogWarning("Gemini response has no candidates.");
                return string.Empty;
            }

            var firstCandidate = candidates[0];
            if (!firstCandidate.TryGetProperty("content", out var content) ||
                !content.TryGetProperty("parts", out var parts) ||
                parts.GetArrayLength() == 0)
            {
                _logger.LogWarning("Gemini response has no content parts.");
                return string.Empty;
            }

            var textPart = parts[0];

            if (!textPart.TryGetProperty("text", out var textElement))
            {
                _logger.LogWarning("Gemini response part has no text field.");
                return string.Empty;
            }

            var summary = textElement.GetString() ?? string.Empty;
            _logger.LogDebug("Gemini summary (preview): {Preview}", summary.Length > 200 ? summary[..200] + "..." : summary);
            return summary;
        }
    }
}
