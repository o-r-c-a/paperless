using Paperless.Application.Interfaces;
using Paperless.Infrastructure.Messaging;
using Paperless.Contracts.Options;

namespace Paperless.Rest.IoC.Extensions
{
    public static class MessagingServiceCollectionExtensions
    {
        /// <summary>
        /// Registers RabbitMQ related options and the publisher client.
        /// </summary>
        public static IServiceCollection AddMessaging(this IServiceCollection services, IConfiguration configuration)
        {
            services.Configure<RabbitMqOptions>(configuration.GetSection(RabbitMqOptions.SectionName));
            services.AddSingleton<IRabbitMqPublisher, RabbitMqPublisher>();
            return services;
        }
    }
}
