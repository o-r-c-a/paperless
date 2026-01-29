using Paperless.Infrastructure.DependencyInjection;
using Paperless.Shared.Utils;
using Serilog;

namespace Paperless.GenAiWorker
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            // Configure Serilog
            Log.Logger = new LoggerConfiguration()
                .ReadFrom.Configuration(new ConfigurationBuilder()
                    .AddJsonFile("appsettings.json", optional: true)
                    .AddEnvironmentVariables()
                    .Build())
                .WriteTo.Console()
                .WithPaperlessDefaults()
                .CreateLogger();

            try
            {
                var builder = Host.CreateDefaultBuilder(args)
                    .UseSerilog()
                    .ConfigureServices((hostContext, services) =>
                    {
                        services.AddInfrastructure(hostContext.Configuration);
                        services.AddHostedService<GenAiWorker>();
                    });
                await builder.Build().RunAsync();
            }
            finally
            {
                Log.CloseAndFlush();
            }
        }
    }
}
