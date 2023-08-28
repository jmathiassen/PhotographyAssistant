using Microsoft.Extensions.Hosting;
using PhotographyAssistant.Classes;
using PhotographyAssistant.Modules;
using Timer = System.Timers.Timer;

namespace PhotographyAssistant.Services;

public class LocalService : IHostedService
{
	private readonly Config config;
	private readonly Timer mainTimer;

	private readonly Import import;
	private readonly Demux demux;
	private readonly TransferExternalHD transferExternalHD;

	public LocalService(Config config)
	{
		this.config = config;
		import = new(config);
		demux = new(config);
		transferExternalHD = new(config);

		mainTimer = new() {
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
			do
			{
				// Importing
				foreach (string importDirectory in config.import.importDirectories)
					if (!Directory.Exists(importDirectory))
						Directory.CreateDirectory(importDirectory);

				if (!Directory.Exists(config.import.incomingDirectory))
					Directory.CreateDirectory(config.import.incomingDirectory);
				if (!Directory.Exists(config.import.promoteDirectory))
					Directory.CreateDirectory(config.import.promoteDirectory);

				// Demuxing
				foreach (ExternalHD externalEntry in config.external.Where(x => x.active))
				{
					if (!Directory.Exists(externalEntry.incoming))
						Directory.CreateDirectory(externalEntry.incoming);
					if (!Directory.Exists(externalEntry.promote))
						Directory.CreateDirectory(externalEntry.promote);
				}
				foreach (RemoteHost remoteEntry in config.remote.Where(x => x.active))
				{
					if (!Directory.Exists(remoteEntry.incoming))
						Directory.CreateDirectory(remoteEntry.incoming);
					if (!Directory.Exists(remoteEntry.promote))
						Directory.CreateDirectory(remoteEntry.promote);
				}

				// External
				foreach (var directory in config.external.Where(x => x.active))
					if (!Directory.Exists(directory.promote))
						Directory.CreateDirectory(directory.promote);

				if (import.NeedsProcessing()) import.Process();
				else if (demux.NeedsProcessing()) demux.Process();
				else if (transferExternalHD.NeedsProcessing()) transferExternalHD.Process();
			} while (
				import.NeedsProcessing() ||
				demux.NeedsProcessing() ||
				transferExternalHD.NeedsProcessing()
			);
		}
		catch (Exception ex)
		{
			Logger.Log(GetType().Name, $"Problem during service check: {ex.Message} {ex.StackTrace}");
		}
		mainTimer.Enabled = true;
	}
}
