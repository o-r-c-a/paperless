using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Paperless.Application.Interfaces;
using Paperless.Contracts.Options;
using Paperless.Infrastructure.Persistence;

namespace Paperless.Rest.IoC.Extensions
{
    public static class InfrastructureInitializer
    {
        public static async Task EnsureInfrastructureInitializedAsync(this WebApplication app)
        {
            using var scope = app.Services.CreateScope();
            var services = scope.ServiceProvider;
            var logger = services.GetRequiredService<ILogger<Program>>();

            try
            {
                logger.LogInformation("Initializing Infrastructure...");

                // apply database migrations
                var context = services.GetRequiredService<PaperlessDbContext>();
                // Only migrate if we are actually using a relational DB (not InMemory)
                if (context.Database.IsRelational())
                {
                    await context.Database.MigrateAsync();
                    logger.LogInformation("Database migrated successfully.");
                }

                // ensuring MinIO bucket exists
                var storage = services.GetRequiredService<IObjectStorageService>();
                var minioOptions = services.GetRequiredService<IOptions<MinioOptions>>().Value;

                await storage.EnsureBucketAsync(minioOptions.Bucket, CancellationToken.None);
                logger.LogInformation("Object Storage (MinIO) initialized. Bucket: {Bucket}", minioOptions.Bucket);
            }
            catch (Exception ex)
            {
                // we log the error but don't crash the app
                logger.LogCritical(ex, "An error occurred while initializing infrastructure.");
            }
        }
    }
}
