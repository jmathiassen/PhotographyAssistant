using System.Text.Json.Serialization;
using System.Text.Json;
using PhotographyAssistant.Classes;
using Renci.SshNet;

namespace PhotographyAssistant.Modules;

public class Config
{
	[JsonIgnore] public string configFilePath { get; set; } = "config.json";
	[JsonIgnore] public DateTime configLastUpdated { get; set; } = DateTime.MinValue;

	public ImportModule import { get; set; } = new();
    public List<ExternalHD> external { get; set; } = [new ExternalHD()];
    public List<RemoteHost> remote { get; set; } = [new RemoteHost()];

    public void ReadConfig()
    {
		FileInfo configFile = new(configFilePath);
        if (!configFile.Exists)
        {
            Logger.Log(GetType().Name, $"Config file {configFilePath} not found, creating with defaults");
            File.WriteAllText(configFilePath, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
            return;
        }

		if(configFile.LastWriteTime == configLastUpdated)
			return;

		Logger.Log(GetType().Name, $"Config file read");

		try
		{
            Config? tmpConfig = JsonSerializer.Deserialize<Config>(File.ReadAllText(configFilePath));
            if (tmpConfig != null)
            {
                foreach (RemoteHost newHost in tmpConfig.remote)
                {
                    RemoteHost? existingHost = remote.Where(x => x.host == newHost.host).FirstOrDefault();

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
        }
        catch (Exception ex)
        {
            Logger.Log(GetType().Name, $"Problem reading config file {configFilePath}: {ex.Message}");
        }
    }
}

public class ImportModule
{
    public List<string> importDirectories { get; set; } = ["import"];
    public string rootDirectory { get; set; } = "spool/import";
    [JsonIgnore] public string incomingDirectory { get { return $"{rootDirectory}/incoming"; } }
    [JsonIgnore] public string promoteDirectory { get { return $"{rootDirectory}/promote"; } }
    public FileTypeHandling[] fileTypes { get; set; } =
    [
        new FileTypeHandling(".jpg"),
        new FileTypeHandling(".jpeg"),
        new FileTypeHandling(".nef"),
        new FileTypeHandling(".nrw"),
        new FileTypeHandling(".png"),
        new FileTypeHandling(".gpx"),
        new FileTypeHandling(".mp4", keepOriginalFilename:false),
        new FileTypeHandling(".mov", keepOriginalFilename:false)
    ];
}
public class FileTypeHandling(string extension, bool dateRename = true, bool keepOriginalFilename = true)
{
    public string extension { get; set; } = extension;
    public bool dateRename { get; set; } = dateRename;
    public bool keepOriginalFilename { get; set; } = keepOriginalFilename;
}
public class ExternalHD
{
    public string root { get; set; } = "spool/external/drive1";
    [JsonIgnore] public string incoming { get { return $"{root}/incoming"; } }
    [JsonIgnore] public string promote { get { return $"{root}/promote"; } }
    public string storage { get; set; } = "storage/drive1";
    public bool active { get; set; } = false;
}

public class RemoteHost
{
    [JsonIgnore] public ScpClient? scpClient { get; set; }
    [JsonIgnore] public DateTime connectionCheckTime { get; set; }
    public string root { get; set; } = "spool/remote/host1";
    [JsonIgnore] public string incoming { get { return $"{root}/incoming"; } }
    [JsonIgnore] public string promote { get { return $"{root}/promote"; } }
    public string username { get; set; } = "username";
    public string pubKeyPath { get; set; } = "host1.pem";
    public string host { get; set; } = "127.0.0.1";
    public int port { get; set; } = 22;
    public string directory { get; set; } = "files";
    public bool active { get; set; } = false;
}
