using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Minio;
using Paperless.Application.Interfaces;
using Paperless.Contracts.Options;
using Paperless.Domain.Repositories;
using Paperless.Infrastructure.Messaging;
using Paperless.Infrastructure.Persistence;
using Paperless.Infrastructure.Repositories;
using Paperless.Infrastructure.Search;
using Paperless.Infrastructure.Storage;

namespace Paperless.Infrastructure.DependencyInjection
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
        {
            services.AddDatabase(configuration);
            services.AddStorage(configuration);
            services.AddMessaging(configuration);
            services.AddHttpClient<ISearchRepository, ElasticSearchRepository>();
            services.Configure<GeminiOptions>(configuration.GetSection(GeminiOptions.SectionName));
            services.Configure<ElasticSearchOptions>(configuration.GetSection(ElasticSearchOptions.SectionName));
            return services;
        }

        private static IServiceCollection AddDatabase(this IServiceCollection services, IConfiguration configuration)
        {
            services.AddDbContext<PaperlessDbContext>(options =>
                options.UseNpgsql
                (
                    configuration.GetConnectionString("DefaultConnection"),
                    npg =>
                    {
                        npg.EnableRetryOnFailure
                        (
                            maxRetryCount: 10,
                            maxRetryDelay: TimeSpan.FromSeconds(30),
                            errorCodesToAdd: null
                        );
                        npg.CommandTimeout(180);
                    }
                )
            );
            services.AddScoped<IDocumentRepository, EfDocumentRepository>();
            services.AddScoped<IDocumentDailyAccessRepository, EfDocumentDailyAccessRepository>();
            return services;
        }
        private static IServiceCollection AddStorage(this IServiceCollection services, IConfiguration configuration)
        {
            // Storage
            services.Configure<MinioOptions>(configuration.GetSection(MinioOptions.SectionName));
            services.AddSingleton<IMinioClient>(sp =>
            {
                var o = sp.GetRequiredService<IOptions<MinioOptions>>().Value;
                return new MinioClient()
                    .WithEndpoint(o.Endpoint, o.Port)
                    .WithCredentials(o.AccessKey, o.SecretKey)
                    .Build();
            });
            services.AddSingleton<IObjectStorageService, MinioStorageService>();
            return services;
        }
        private static IServiceCollection AddMessaging(this IServiceCollection services, IConfiguration configuration)
        {
            // Messaging
            services.Configure<RabbitMqOptions>(configuration.GetSection(RabbitMqOptions.SectionName));
            services.AddSingleton<IRabbitMqPublisher, RabbitMqPublisher>();

            return services;
        }
    }
}
