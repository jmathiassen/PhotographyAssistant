using Microsoft.Extensions.Hosting;
using PhotographyAssistant.Classes;
using PhotographyAssistant.Modules;
using Timer = System.Timers.Timer;

namespace PhotographyAssistant.Services;

public class RemoteService : IHostedService
{
	private readonly Config config;
	private readonly Timer mainTimer;

	private readonly TransferRemote transferRemote;

	public RemoteService(Config config)
	{
		this.config = config;
		transferRemote = new TransferRemote(config);

		mainTimer = new Timer {
			Interval = 1000,
			AutoReset = false
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
		mainTimer.Enabled = false;
		try
		{
			// Remote
			foreach (string promoteDirectory in config.Access(c => c.remote).Select(remoteHost => remoteHost.promote))
				if (!Directory.Exists(promoteDirectory))
					Directory.CreateDirectory(promoteDirectory);

			do {
				if (transferRemote.NeedsProcessing()) transferRemote.Process();
			} while (transferRemote.NeedsProcessing(true));
		}
		catch (Exception ex)
		{
			Logger.Log(GetType().Name, $"Problem during service check: {ex.Message} {ex.StackTrace}");
		}
		mainTimer.Enabled = true;
	}
}
