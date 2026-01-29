using Minio;
using Paperless.Application.Interfaces;
using Paperless.Infrastructure.Storage;
using Paperless.Contracts.Options;

namespace Paperless.Rest.IoC.Extensions
{
    public static class StorageServiceCollectionExtensions
    {
        /// <summary>
        /// Registers Minio options and the Minio client + object storage service.
        /// </summary>
        public static IServiceCollection AddStorage(this IServiceCollection services, IConfiguration configuration)
        {
            services.Configure<MinioOptions>(configuration.GetSection(MinioOptions.SectionName));

            services.AddSingleton<IMinioClient>(sp =>
            {
                var o = sp.GetRequiredService<MinioOptions>();
                return new MinioClient()
                    .WithEndpoint(o.Endpoint, o.Port)
                    .WithCredentials(o.AccessKey, o.SecretKey)
                    .Build();
            });
            services.AddSingleton<IObjectStorageService, MinioStorageService>();
            return services;
        }
    }
}
