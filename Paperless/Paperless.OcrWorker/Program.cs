using Paperless.OcrWorker;
using Paperless.Shared.Utils;
using Serilog;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Configuration;
using Paperless.Infrastructure.DependencyInjection;

// Configure Serilog
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(new ConfigurationBuilder()
        .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
        .Build())
    .WithPaperlessDefaults()
    .CreateLogger();

var host = Host.CreateDefaultBuilder(args)
    .UseSerilog((context, services, config) =>
    {
        config.ReadFrom.Configuration(context.Configuration)
              .ReadFrom.Services(services)
              .WithPaperlessDefaults();
    })
    .ConfigureServices((ctx, services) =>
    {
        // registering background worker as hosted service
        services.AddInfrastructure(ctx.Configuration);
        services.AddHostedService<OcrWorker>();
    })
    .Build();

await host.RunAsync();
