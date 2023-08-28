using PhotographyAssistant.Classes;
using PhotographyAssistant.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using PhotographyAssistant.Modules;

Config config = new();

IHost host = Host.CreateDefaultBuilder(args)
	.ConfigureLogging(builder => builder.SetMinimumLevel(LogLevel.Warning))
	.ConfigureServices(services =>
	{
		services.AddHostedService(sp => new ConfigService(config));
		services.AddHostedService(sp => new LocalService(config));
		services.AddHostedService(sp => new RemoteService(config));
	})
	.Build();
Logger.Log("Main", "Starting");
await host.RunAsync();
Logger.Log("Main", "Shutting down");
