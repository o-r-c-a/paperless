using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Minio;
using Minio.DataModel.Args;
using NSubstitute;
using Paperless.Contracts.Messages;
using Paperless.Contracts.Options;
using System.Reflection;
using System.Text;
using Paperless.OcrWorker;

namespace Paperless.OcrWorker.Tests
//namespace Paperless.Tests.OcrWorker
{
    [TestFixture]
    public class OcrWorker_FailurePaths_Tests
    {
        private OcrWorker CreateWorkerWithInjectedMinioAndLogger(IMinioClient mockMinioClient, ILogger<OcrWorker> logger)
        {
            var rabbitOpts = Substitute.For<IOptions<RabbitMqOptions>>();
            rabbitOpts.Value.Returns(new RabbitMqOptions
            {
                Host = "localhost",
                OcrInQueue = "in",
                OcrOutQueue = "out"
            });

            var minioOpts = Substitute.For<IOptions<MinioOptions>>();
            minioOpts.Value.Returns(new MinioOptions
            {
                Endpoint = "localhost",
                Port = 9000,
                AccessKey = "dummy",
                SecretKey = "dummy",
                Bucket = "paperless"
            });

            var worker = new OcrWorker(rabbitOpts, minioOpts, logger);
            var field = typeof(OcrWorker).GetField("_minioClient", BindingFlags.Instance | BindingFlags.NonPublic);


            Assert.That(field, Is.Not.Null, "Could not find _minioClient field via reflection.");
            field!.SetValue(worker, mockMinioClient);

            //var worker = (OcrWorker)Activator.CreateInstance(typeof(OcrWorker), logger)!;
            //var field = typeof(OcrWorker).GetField("_minioClient", BindingFlags.Instance | BindingFlags.NonPublic);
            //Assert.That(field, Is.Not.Null, "Could not find _minioClient field via reflection.");
            //field!.SetValue(worker, minioClient);

            return worker;
        }

        // Helper: create a substitute IMinioClient that writes `bytes` to the callback stream
        private IMinioClient CreateMinioClientThatServesBytes(byte[] bytes)
        {
            var minio = Substitute.For<IMinioClient>();

            minio
                .When(x => x.GetObjectAsync(Arg.Any<GetObjectArgs>(), Arg.Any<CancellationToken>()))
                .Do(async ci =>
                {
                    var args = ci.ArgAt<GetObjectArgs>(0);

                    // Try to access the internal callback field
                    var callbackField = typeof(GetObjectArgs).GetField("callbackStream", BindingFlags.Instance | BindingFlags.NonPublic);

                    if (callbackField != null)
                    {
                        var callbackObj = callbackField.GetValue(args);

                        if (callbackObj is Func<Stream, Task> asyncCb)
                        {
                            using var ms = new MemoryStream(bytes);
                            await asyncCb(ms);
                            return;
                        }

                        if (callbackObj is Action<Stream> syncCb)
                        {
                            using var ms = new MemoryStream(bytes);
                            syncCb(ms);
                            return;
                        }
                    }

                    // Fallback: do nothing
                    await Task.CompletedTask;
                });

            return minio;
        }

        [Test]
        public async Task ProcessOcrJobAsync_ImageExtraction_Fails_LogsError_AndReturnsEmpty()
        {
            // Arrange
            var logger = Substitute.For<ILogger<OcrWorker>>();
            var imageBytes = Encoding.UTF8.GetBytes("not-a-real-image");
            var minio = CreateMinioClientThatServesBytes(imageBytes);

            var worker = CreateWorkerWithInjectedMinioAndLogger(minio, logger);

            var job = new OcrJobMessage
            {
                Id = Guid.NewGuid(),
                Name = "test.jpg",
                ContentType = "image/jpeg",
                SizeBytes = imageBytes.Length,
                UploadedAt = DateTime.UtcNow
            };

            var method = typeof(OcrWorker).GetMethod("ProcessOcrJobAsync", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null);

            // Act
            var ct = CancellationToken.None;
            var task = (Task<string>)method!.Invoke(worker, new object[] { job, ct })!;
            var result = await task;

            // Assert
            Assert.That(result, Is.EqualTo(string.Empty));

            Assert.That(
                HasLogLevel(logger, LogLevel.Error) || HasMessageContaining(logger, "OCR completed"),
                Is.True,
                "Expected either an Error log or at least an OCR completion log."
            );
        }

        [Test]
        public async Task ProcessOcrJobAsync_PdfExtraction_ReturnsEmpty_AndCleansUpTempFile()
        {
            // Arrange
            var logger = Substitute.For<ILogger<OcrWorker>>();
            var pdfBytes = Encoding.UTF8.GetBytes("%PDF-1.0\n%minimal\n");
            var minio = CreateMinioClientThatServesBytes(pdfBytes);

            var worker = CreateWorkerWithInjectedMinioAndLogger(minio, logger);

            var job = new OcrJobMessage
            {
                Id = Guid.NewGuid(),
                Name = "test.pdf",
                ContentType = "application/pdf",
                SizeBytes = pdfBytes.Length,
                UploadedAt = DateTime.UtcNow
            };

            var method = typeof(OcrWorker).GetMethod("ProcessOcrJobAsync", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null);

            // Act
            var ct = CancellationToken.None;
            var task = (Task<string>)method!.Invoke(worker, new object[] { job, ct })!;
            var result = await task;

            // Assert
            Assert.That(result, Is.EqualTo(string.Empty));

            var expectedTempPath = Path.Combine(Path.GetTempPath(), job.Id + ".pdf");
            Assert.That(File.Exists(expectedTempPath), Is.False, "Temporary PDF file should be deleted.");

            Assert.That(
                HasMessageContaining(logger, "Deleted temporary file") || HasMessageContaining(logger, "OCR completed"),
                Is.True,
                "Expected a log indicating OCR completion and/or temp file cleanup."
            );
        }

        private static bool HasLogLevel(ILogger<OcrWorker> logger, LogLevel level)
        {
            return logger.ReceivedCalls().Any(c =>
            {
                if (c.GetMethodInfo().Name != nameof(ILogger.Log)) return false;
                var args = c.GetArguments();
                return args.Length > 0 && args[0] is LogLevel ll && ll == level;
            });
        }

        private static bool HasMessageContaining(ILogger<OcrWorker> logger, string contains)
        {
            return logger.ReceivedCalls().Any(c =>
            {
                if (c.GetMethodInfo().Name != nameof(ILogger.Log)) return false;

                var args = c.GetArguments();
                if (args.Length < 3) return false;

                var state = args[2];
                var msg = state?.ToString() ?? string.Empty;
                return msg.Contains(contains, StringComparison.OrdinalIgnoreCase);
            });
        }
    }
}
