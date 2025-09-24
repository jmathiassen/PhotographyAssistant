using System.Text;
using System.Text.Json.Serialization;
using System.Text.Json;
using PhotographyAssistant.Classes;
using Renci.SshNet;

namespace PhotographyAssistant.Modules;

public record Config
{
	private static readonly object ConfigLock = new();
	[JsonIgnore] public string configFilePath => "config.json";
	[JsonIgnore] public DateTime configLastUpdated { get; set; } = DateTime.MinValue;

	public ImportModule import { get; set; } = new();
	public List<ExternalHD> external { get; set; } = [new()];
	public List<RemoteHost> remote { get; set; } = [new()];

	public void ReadConfig()
	{
		FileInfo configFile = new(configFilePath);
		lock (ConfigLock)
		{
			if (!configFile.Exists)
			{
				Logger.Log(GetType().Name, $"Config file {configFilePath} not found, creating with defaults");
				File.WriteAllText(configFilePath, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
				return;
			}

			if (configFile.LastWriteTime == configLastUpdated)
				return;

			Logger.Log(GetType().Name, $"Config file read");

			try
			{
				Config? tmpConfig = JsonSerializer.Deserialize<Config>(File.ReadAllText(configFilePath));
				if (tmpConfig != null)
				{
					foreach (RemoteHost newHost in tmpConfig.remote)
					{
						RemoteHost? existingHost = remote.FirstOrDefault(x => x.host == newHost.host);

						if (existingHost != null &&
						    existingHost.scpClient != null &&
						    existingHost.host == newHost.host &&
						    existingHost.port == newHost.port &&
						    existingHost.username == newHost.username &&
						    existingHost.pubKeyPath == newHost.pubKeyPath)
						{
							newHost.scpClient = existingHost.scpClient;
							newHost.connectionCheckTime = existingHost.connectionCheckTime;
						}
					}

					import = tmpConfig.import;
					external = tmpConfig.external;
					remote = tmpConfig.remote;
				}

				configLastUpdated = configFile.LastWriteTime;


				// --- Startup Report Logic ---
				StringBuilder report = new();
				report.AppendLine("\n--- Running Configuration ---");

				// Import Settings
				report.AppendLine($"  Import sources: {string.Join(", ", import.importDirectories)}");
				report.AppendLine($"  Import spool path: {import.rootDirectory}");

				// External HDs
				report.AppendLine($"  Active External HD Groups ({external.Count(hd => hd.active)}):");
				foreach (ExternalHD activeHd in external.Where(hd => hd.active))
				{
					report.AppendLine($"    Drive group: {activeHd.root}");
					report.AppendLine($"      Paths:");
					foreach (string physicalPath in activeHd.physicalPaths)
						report.AppendLine($"      - {physicalPath}");
				}
				report.AppendLine($"  Inactive External HD Groups ({external.Count(hd => !hd.active)}):");
				foreach (ExternalHD inactiveHd in external.Where(hd => !hd.active))
				{
					report.AppendLine($"    Drive group: {inactiveHd.root}");
					report.AppendLine($"      Paths:");
					foreach (string physicalPath in inactiveHd.physicalPaths)
						report.AppendLine($"      - {physicalPath}");
				}

				// Remote Hosts
				List<RemoteHost> activeRemote = remote.Where(h => h.active).ToList();
				if (activeRemote.Any())
				{
					report.AppendLine($"  Active Remote Hosts ({activeRemote.Count}):");
					foreach (RemoteHost host in activeRemote)
						report.AppendLine($"    - Host: {host.host}, User: {host.username}, Path: {host.directory}");
				}
				else
					report.AppendLine("  Remote Hosts: Inactive");

				report.Append("---------------------------");
				Logger.Log(GetType().Name, report.ToString());
			}
			catch (Exception ex)
			{
				Logger.Log(GetType().Name, $"Problem reading config file {configFilePath}: {ex.Message}");
			}
		}

	}

	// Helper method to allow other services to safely access the config
	public T Access<T>(Func<Config, T> accessFunc)
	{
		lock (ConfigLock)
		{
			return accessFunc(this);
		}
	}

	// Helper method to allow other services to safely access the config without a return value
	public void Access(Action<Config> accessAction)
	{
		lock (ConfigLock)
		{
			accessAction(this);
		}
	}
}

public record ImportModule
{
	public List<string> importDirectories { get; set; } = ["import"];
	public string externalHardDiskIdentifier { get; set; } = ".exclude_this_drive";
	public string rootDirectory { get; set; } = "data/spool/import";
	[JsonIgnore] public string incomingDirectory => $"{rootDirectory}/incoming";
	[JsonIgnore] public string promoteDirectory => $"{rootDirectory}/promote";

	public FileTypeHandling[] fileTypes { get; set; } =
	[
		new(".jpg"),
		new(".jpeg"),
		new(".nef"),
		new(".nrw"),
		new(".png"),
		new(".gpx"),
		new(".mp4", keepOriginalFilename: false),
		new(".mov", keepOriginalFilename: false)
	];
}

public record FileTypeHandling(string extension, bool dateRename = true, bool keepOriginalFilename = true)
{
	public string extension { get; set; } = extension;
	public bool dateRename { get; set; } = dateRename;
	public bool keepOriginalFilename { get; set; } = keepOriginalFilename;
}

public record ExternalHD
{
	public string root { get; set; } = "data/spool/external/drive1";

	[JsonIgnore] public string incoming => $"{root}/incoming";
	[JsonIgnore] public string promote => $"{root}/promote";
	public List<string> physicalPaths { get; set; } = ["data/storage/hdd1", "data/storage/ssd1"];
	public bool active { get; set; } = false;
	public bool hasInsufficientSpace { get; set; } = false;
}

public record RemoteHost
{
	[JsonIgnore] public ScpClient? scpClient { get; set; }
	[JsonIgnore] public DateTime connectionCheckTime { get; set; }
	public string root { get; set; } = "data/spool/remote/host1";
	[JsonIgnore] public string incoming => $"{root}/incoming";
	[JsonIgnore] public string promote => $"{root}/promote";
	public string username { get; set; } = "username";
	public string pubKeyPath { get; set; } = "host1.pem";
	public string host { get; set; } = "127.0.0.1";
	public int port { get; set; } = 22;
	public string directory { get; set; } = "files";
	public bool active { get; set; } = false;
}