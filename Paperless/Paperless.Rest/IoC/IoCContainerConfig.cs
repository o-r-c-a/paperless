using Paperless.Rest.IoC.Extensions;

namespace Paperless.Rest.IoC
{
    public static class IoCContainerConfig
    {
        /// <summary>
        /// Currently not being used!
        /// Registers all IoC modules in the correct order.
        /// </summary>
        public static IServiceCollection AddIoCContainerConfig(this IServiceCollection services, IConfiguration cfg)
        {
            services.AddDatabase(cfg);
            services.AddStorage(cfg);
            services.AddMessaging(cfg);
            services.AddApplicationServices();
            return services;
        }
    }
}
