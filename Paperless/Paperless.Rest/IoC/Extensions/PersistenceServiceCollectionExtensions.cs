using Microsoft.EntityFrameworkCore;
using Paperless.Domain.Repositories;
using Paperless.Infrastructure.Persistence;
using Paperless.Infrastructure.Repositories;

namespace Paperless.Rest.IoC.Extensions
{
    public static class PersistenceServiceCollectionExtensions
    {
        /// <summary>
        /// Registers the DbContext and persistence-level services (repositories).
        /// </summary>
        public static IServiceCollection AddDatabase(this IServiceCollection services, IConfiguration configuration)
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

            // Repository lifetimes: scoped so they share the same DbContext per request
            services.AddScoped<IDocumentRepository, EfDocumentRepository>();
            return services;
        }
    }
}
