using Microsoft.Extensions.Hosting;
using Paperless.IndexWorker;
using Paperless.Infrastructure.DependencyInjection;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddHostedService<IndexWorker>();

var host = builder.Build();
host.Run();