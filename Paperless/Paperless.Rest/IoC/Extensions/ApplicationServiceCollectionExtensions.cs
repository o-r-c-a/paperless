using FluentValidation;
using Paperless.Application.Interfaces;
using Paperless.Application.Mapper;
using Paperless.Application.Services;
using Paperless.Rest.Controllers;
using Paperless.Rest.Mapper;
using Paperless.Rest.Messaging;
using Paperless.Rest.Properties;
using Paperless.Rest.Validators;
using System.Text.Json.Serialization;
using MediatR;

namespace Paperless.Rest.IoC.Extensions
{
    public static class ApplicationServiceCollectionExtensions
    {
        /// <summary>
        /// Registers application-layer services, AutoMapper, validators, controllers and Swagger.
        /// </summary>
        public static IServiceCollection AddApplicationServices(this IServiceCollection services)
        {
            // AutoMapper
            services.AddAutoMapper(cfg =>
            {
                cfg.AddMaps
                (
                    typeof(DocumentProfile).Assembly,
                    typeof(RequestDocumentProfile).Assembly
                );
            });

            // Application services: scoped so they share DbContext within a request
            services.AddScoped<IDocumentService, DocumentService>();

            // MediatR
            services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(DocumentService).Assembly));

            // Validators
            services.AddValidatorsFromAssemblyContaining<CreateDocumentRequestValidator>();
            services.AddValidatorsFromAssemblyContaining<UpdateDocumentRequestValidator>();

            // Controllers + JSON options (prevent cycles with navigation properties)
            services.AddControllers()
                .AddJsonOptions(options =>
                {
                    options.JsonSerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles;
                })
                .AddApplicationPart(typeof(DocumentsController).Assembly);

            services.AddHostedService<SummaryConsumer>();
            services.AddHostedService<OcrResultFanoutConsumer>();

            services.AddEndpointsApiExplorer();
            services.AddSwaggerGen(c => c.SchemaFilter<DefaultBodyFilter>());
            return services;
        }
    }
}
