namespace Paperless.Contracts.Options
{
    public class RabbitMqOptions
    {
        public const string SectionName = "RabbitMq";
        public string Host { get; init; } = "rabbitmq";
        public int Port { get; init; } = 5672;
        public string Username { get; init; } = "guest";
        public string Password { get; init; } = "guest";
        // Queue Definitions
        public string OcrInQueue { get; init; } = "paperless.ocr.in";
        public string OcrOutQueue { get; init; } = "paperless.ocr.out";
        public string GenaiInQueue { get; init; } = "paperless.genai.in";
        public string SummaryInQueue { get; init; } = "paperless.summary.in";
        public string IndexInQueue { get; init; } = "paperless.index.in";
        public string IndexUpdateQueue { get; init; } = "paperless.index.update";
        public string IndexDeleteQueue { get; init; } = "paperless.index.delete";
        public string Vhost { get; init; } = "/";
        // Retry Policies
        public int ConnectionRetries { get; init; } = 30;
        public int ConnectionRetryDelaySeconds { get; init; } = 2;
    }
}
