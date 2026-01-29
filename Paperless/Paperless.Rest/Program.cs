using Paperless.Infrastructure.DependencyInjection;
using Paperless.Rest.IoC.Extensions;
using Paperless.Rest.Middleware;
using Paperless.Shared.Utils;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// Configure Serilog
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)   // reads from appsettings.json
    .WithPaperlessDefaults()
    .CreateLogger();

builder.Host.UseSerilog();

// Explicit DI module registration (ordering matters)
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddApplicationServices();
builder.Services.AddHttpClient();

var app = builder.Build();

//app.UseApiExceptionMiddleware();
app.UseMiddleware<ApiExceptionMiddleware>();

var uploadRoot = Path.Combine("/app", "uploads");
Directory.CreateDirectory(uploadRoot);

//Initialize Infrastructure(DB Migration, MinIO Bucket Creation, etc.)
await app.EnsureInfrastructureInitializedAsync();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSerilogRequestLogging(); // more verbose logging - status codes (useful to see 4xx/5xx)
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapControllers();
app.Run();