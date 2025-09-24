using Microsoft.Extensions.Hosting;
using PhotographyAssistant.Classes;
using PhotographyAssistant.Modules;
using Timer = System.Timers.Timer;

namespace PhotographyAssistant.Services;

public class ConfigService : IHostedService
{
	private readonly Config config;
	private readonly Timer mainTimer;

	public ConfigService(Config config)
	{
		this.config = config;
		config.ReadConfig();
		mainTimer = new Timer {
			Interval = 1000
		};
		mainTimer.Elapsed += Process;
	}
	public async Task StartAsync(CancellationToken stoppingToken)
	{
		Logger.Log(GetType().Name, "Starting");
		await Task.Run(() => mainTimer.Start(), stoppingToken);
		Logger.Log(GetType().Name, "Started");
	}
	public async Task StopAsync(CancellationToken stoppingToken)
	{
		Logger.Log(GetType().Name, "Stopping");
		await Task.Run(() => mainTimer.Stop(), stoppingToken);
		Logger.Log(GetType().Name, "Stopped");
	}
	private void Process(object? source, System.Timers.ElapsedEventArgs e)
	{
		try
		{
			config.ReadConfig();
		}
		catch (Exception ex)
		{
			Logger.Log(GetType().Name, $"Problem during config read: {ex.Message} {ex.StackTrace}");
		}
	}
}
