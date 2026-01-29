using Microsoft.Extensions.Options;
using Minio;
using Minio.DataModel.Args;
using Paperless.Contracts.Messages;
using Paperless.Contracts.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Text;
using System.Text.Json;
using Tesseract;

namespace Paperless.OcrWorker
{
    public sealed class OcrWorker : BackgroundService
    {
        // RabbitMQ settings
        private readonly RabbitMqOptions _rabbitMqOptions;
        private readonly MinioOptions _minioOptions;
        private readonly ILogger<OcrWorker> _logger;
        private readonly IMinioClient _minioClient;
        public OcrWorker(
            IOptions<RabbitMqOptions> rabbitMqOptions,
            IOptions<MinioOptions> minioOptions,
            ILogger<OcrWorker> logger)
        {
            _rabbitMqOptions = rabbitMqOptions.Value;
            _minioOptions = minioOptions.Value;
            _logger = logger;
            // Initialise MinIO client
            _minioClient = new MinioClient()
                .WithEndpoint($"{_minioOptions.Endpoint}:{_minioOptions.Port}")
                .WithCredentials(_minioOptions.AccessKey, _minioOptions.SecretKey)
                .Build();
        }
        protected override async Task ExecuteAsync(CancellationToken ct)
        {
            await Task.Delay(2000, ct); // slight delay to ensure dependencies are up
            var factory = new ConnectionFactory
            {
                HostName = _rabbitMqOptions.Host,
                Port = _rabbitMqOptions.Port,
                UserName = _rabbitMqOptions.Username,
                Password = _rabbitMqOptions.Password
            };

            IConnection connection = null!;
            var maxAttempts = _rabbitMqOptions.ConnectionRetries;          // ~30 * 2s = 60s total wait
            var delay = TimeSpan.FromSeconds(_rabbitMqOptions.ConnectionRetryDelaySeconds);

            for (var attempt = 1; attempt <= maxAttempts; attempt++)
            {
                try
                {
                    connection = await factory.CreateConnectionAsync(cancellationToken: ct);
                    _logger.LogInformation("Connected to RabbitMQ on attempt {Attempt}/{Max}.", attempt, maxAttempts);
                    break;
                }
                catch (Exception ex) when (!ct.IsCancellationRequested)
                {
                    _logger.LogWarning(ex,
                        "RabbitMQ connection failed (attempt {Attempt}/{Max}). Retrying in {Delay}s...",
                        attempt, maxAttempts, delay.TotalSeconds);

                    await Task.Delay(delay, ct);
                }
            }

            if (connection == null)
            {
                throw new InvalidOperationException("Could not connect to RabbitMQ after multiple attempts.");
            }

            await using var _ = connection; // ensures DisposeAsync via await using
            await using var channel = await connection.CreateChannelAsync(cancellationToken: ct);

            await channel.QueueDeclareAsync(
                queue: _rabbitMqOptions.OcrInQueue,
                durable: true,
                exclusive: false,
                autoDelete: false,
                arguments: null,
                cancellationToken: ct);

            await channel.QueueDeclareAsync(
                queue: _rabbitMqOptions.OcrOutQueue,
                durable: true,
                exclusive: false,
                autoDelete: false,
                arguments: null,
                cancellationToken: ct);

            var consumer = new AsyncEventingBasicConsumer(channel);
            consumer.ReceivedAsync += async (_, ea) =>
            {
                try
                {
                    var dto = JsonSerializer.Deserialize<OcrJobMessage>(ea.Body.Span);
                    if (dto == null)
                    {
                        _logger.LogWarning("Received null or invalid OCR job message.");
                        return;
                    }

                    _logger.LogInformation(
                        "Received OCR job Id={Id}, Name={Name}, Type={Type}, Size={SizeBytes}",
                        dto.Id, dto.Name, dto.ContentType, dto.SizeBytes);

                    var text = await ProcessOcrJobAsync(dto, ct);

                    if (!string.IsNullOrWhiteSpace(text))
                    {
                        var result = new OcrResultMessage
                        {
                            Id = dto.Id,
                            Name = dto.Name,
                            ContentType = dto.ContentType,
                            SizeBytes = dto.SizeBytes,
                            UploadedAt = dto.UploadedAt,
                            Tags = dto.Tags,
                            Text = text
                        };

                        var body = JsonSerializer.SerializeToUtf8Bytes(result);

                        var props = new BasicProperties { Persistent = true };

                        await channel.BasicPublishAsync(
                            exchange: "",
                            routingKey: _rabbitMqOptions.OcrOutQueue,
                            mandatory: false,
                            basicProperties: props,
                            body: body,
                            cancellationToken: ct);

                        _logger.LogInformation(
                            "Published OCR result for document {Id} to queue {Queue} (length: {Length} chars)",
                            dto.Id, _rabbitMqOptions.OcrOutQueue, text.Length);
                    }
                    else
                    {
                        _logger.LogWarning("No OCR text extracted for document {Id}, skipping publish.", dto.Id);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error while handling message DeliveryTag={DeliveryTag}", ea.DeliveryTag);
                }
            };

            await channel.BasicConsumeAsync(
                queue: _rabbitMqOptions.OcrInQueue,
                autoAck: true,
                consumer: consumer,
                cancellationToken: ct);

            _logger.LogInformation("Listening on queue '{Queue}' at {Host}:{Port}", _rabbitMqOptions.OcrInQueue, _rabbitMqOptions.Host, _rabbitMqOptions.Port);
            _logger.LogInformation(
                "MinIO configured at {Endpoint} with bucket '{Bucket}'",
                $"{_minioOptions.Endpoint}:{_minioOptions.Port}", _minioOptions.Bucket);
            
            // keeps the process alive
            while (!ct.IsCancellationRequested)
                await Task.Delay(250, ct);
        }
    private async Task<string> ProcessOcrJobAsync(OcrJobMessage job, CancellationToken ct)
        {
            try
            {
                // Determine file extension based on MIME type
                var ext = GetExtensionFromContentType(job.ContentType);
                var objectName = $"{job.Id}{ext}";
                var tempFilePath = Path.Combine(Path.GetTempPath(), objectName);

                _logger.LogInformation("Fetching object {ObjectName} from MinIO bucket {Bucket}", objectName, _minioOptions.Bucket);

                // --- Download the file from MinIO to /tmp ---
                await using (var fileStream = File.Create(tempFilePath))
                {
                    var getArgs = new GetObjectArgs()
                        .WithBucket(_minioOptions.Bucket)
                        .WithObject(objectName)
                        .WithCallbackStream(s =>
                        {
                            // Copy synchronously to avoid async/await disposal race
                            s.CopyTo(fileStream);
                            fileStream.Flush();
                        });

                    await _minioClient.GetObjectAsync(getArgs, ct);
                }

                _logger.LogInformation("Downloaded {ObjectName} to {TempPath}", objectName, tempFilePath);

                // Perform OCR depending on file type
                string extractedText;
                if (IsPdf(job.ContentType))
                {
                    _logger.LogInformation("Performing OCR on PDF document {Id}", job.Id);
                    extractedText = await ExtractTextFromPdfAsync(tempFilePath, ct);
                }
                else if (IsImage(job.ContentType))
                {
                    _logger.LogInformation("Performing OCR on image document {Id}", job.Id);
                    extractedText = ExtractTextFromImage(tempFilePath, ct);
                }
                else if (IsText(job.ContentType))
                {
                    _logger.LogInformation("Reading text file directly {Id}", job.Id);
                    // Simply read the content of the file
                    extractedText = await File.ReadAllTextAsync(tempFilePath, ct);
                }
                else
                {
                    _logger.LogWarning("Unsupported content type {ContentType} for document {Id}", job.ContentType, job.Id);
                    return string.Empty;
                }

                // --- Log and summarize the OCR result ---
                _logger.LogInformation(
                    "OCR completed for document {Id} ({Name}). Extracted {CharCount} characters.",
                    job.Id, job.Name, extractedText.Length);

                // Print only the first 400 characters to avoid spam in the logs
                var preview = extractedText.Length > 400
                    ? extractedText[..400] + "..."
                    : extractedText;
                _logger.LogInformation("Extracted text (preview):\n{Text}", preview);

                // --- Clean up ---
                if (File.Exists(tempFilePath))
                {
                    File.Delete(tempFilePath);
                    _logger.LogDebug("Deleted temporary file {TempPath}", tempFilePath);
                }
                return extractedText;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to process OCR job for document {Id}", job.Id);
                return string.Empty;
            }
        }

        private string ExtractTextFromImage(string imagePath, CancellationToken ct)
        {
            try
            {
                //var tessdataPath = Path.Combine(AppContext.BaseDirectory, "tessdata");
                // Use mounted tessdata path from environment variable
                var tessdataPath = $"{Environment.GetEnvironmentVariable("TESSDATA_PREFIX") ?? "/usr/share"}/tessdata";

                using var engine = new TesseractEngine(tessdataPath, "eng+deu", EngineMode.Default);
                using var img = Pix.LoadFromFile(imagePath);
                using var page = engine.Process(img);

                var text = page.GetText();
                var confidence = page.GetMeanConfidence();

                _logger.LogInformation("OCR confidence: {Confidence:P2}", confidence);

                return text;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error extracting text from image {ImagePath}", imagePath);
                return string.Empty;
            }
        }

        private async Task<string> ExtractTextFromPdfAsync(string pdfPath, CancellationToken ct)
        {
            try
            {
                // Convert PDF to images using Ghostscript, then OCR each page
                var outputDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
                Directory.CreateDirectory(outputDir);

                var outputPattern = Path.Combine(outputDir, "page-%03d.png");

                // Ghostscript command to convert PDF to images
                var gsProcess = new System.Diagnostics.Process
                {
                    StartInfo = new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = "gs",
                        Arguments = $"-dNOPAUSE -dBATCH -sDEVICE=png16m -r300 -sOutputFile=\"{outputPattern}\" \"{pdfPath}\"",
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    }
                };

                gsProcess.Start();
                await gsProcess.WaitForExitAsync(ct);

                if (gsProcess.ExitCode != 0)
                {
                    var error = await gsProcess.StandardError.ReadToEndAsync();
                    _logger.LogError("Ghostscript failed: {Error}", error);
                    return string.Empty;
                }

                // OCR each generated image
                var imageFiles = Directory.GetFiles(outputDir, "*.png").OrderBy(f => f).ToArray();
                var allText = new StringBuilder();

                foreach (var imageFile in imageFiles)
                {
                    var pageText = ExtractTextFromImage(imageFile, ct);
                    allText.AppendLine(pageText);
                    allText.AppendLine("--- Page Break ---");
                }

                // Clean up temporary images
                Directory.Delete(outputDir, true);

                return allText.ToString();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error extracting text from PDF {PdfPath}", pdfPath);
                return string.Empty;
            }
        }

        private string GetExtensionFromContentType(string contentType)
        {
            return contentType.ToLower() switch
            {
                "application/pdf" => ".pdf",
                "image/png" => ".png",
                "image/jpeg" => ".jpg",
                "image/jpg" => ".jpg",
                "image/tiff" => ".tiff",
                "image/bmp" => ".bmp",
                "text/plain" => ".txt",
                _ => ".bin"
            };
        }

        private bool IsPdf(string contentType) =>
            contentType.Equals("application/pdf", StringComparison.OrdinalIgnoreCase);

        private bool IsImage(string contentType) =>
            contentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase);
        private bool IsText(string contentType) =>
            contentType.Equals("text/plain", StringComparison.OrdinalIgnoreCase);
    }
}
