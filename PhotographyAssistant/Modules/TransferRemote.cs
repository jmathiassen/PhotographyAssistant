using PhotographyAssistant.Classes;
using Renci.SshNet;

namespace PhotographyAssistant.Modules;

public class TransferRemote(Config config)
{
	public bool NeedsProcessing(bool checkConnection = false)
	{
		foreach (RemoteHost host in config.Access(c => c.remote).Where(x => x.active))
		{
			if (checkConnection)
			{
				if (Directory.GetFiles(host.promote).Length > 0 && host.scpClient?.IsConnected == true)
					return true;
			}
			else if (Directory.GetFiles(host.promote).Length > 0)
				return true;
		}

		return false;
	}
	public void Process()
	{
		try
		{
			foreach (RemoteHost remoteHost in config.Access(c => c.remote).Where(remoteHost => remoteHost.active))
			{
				FileInfo? fileFrom = new DirectoryInfo(remoteHost.promote).GetFiles().FirstOrDefault();
				if (fileFrom == null)
					continue;

				if (remoteHost.scpClient == null)
				{
					Logger.Log(GetType().Name, $"{remoteHost.host}:{remoteHost.port} - Initialize connection configuration");
					remoteHost.scpClient = new ScpClient(remoteHost.host, remoteHost.port, remoteHost.username, [new PrivateKeyFile(remoteHost.pubKeyPath)]);
				}

				if (!remoteHost.scpClient.IsConnected)
				{
					if (DateTime.Now > remoteHost.connectionCheckTime)
					{
						try
						{
							Logger.Log(GetType().Name, $"{remoteHost.host}:{remoteHost.port} - Open connection");
							remoteHost.scpClient.Connect();
						}
						catch(Exception ex)
						{
							Logger.Log(GetType().Name, $"{remoteHost.host}:{remoteHost.port} - Failure while opening connection: {ex.Message}");
							remoteHost.connectionCheckTime = DateTime.Now.AddSeconds(10);
							continue;
						}
					}
					else
						continue;
				}
				try
				{
					using (FileStream streamFrom = File.OpenRead(fileFrom.FullName))
					{
						remoteHost.scpClient!.Upload(streamFrom, $"{Path.Combine(remoteHost.directory, fileFrom.Name)}");
						Logger.Log(GetType().Name, $"{Path.Combine(remoteHost.promote, fileFrom.Name)} -> {remoteHost.host}:{remoteHost.directory}/{fileFrom.Name} - Uploaded");
					}
					File.Delete(fileFrom.FullName);
					Logger.Log(GetType().Name, $"{Path.Combine(remoteHost.directory, fileFrom.Name)} - Deleted");
				}
				catch (Exception ex)
				{
					Logger.Log(GetType().Name, $"{remoteHost.host}:{remoteHost.port} - Transfer failed: {ex.Message}");
					remoteHost.scpClient.Disconnect();
				}

				if (Directory.GetFiles(remoteHost.promote).Length == 0)
				{
					Logger.Log(GetType().Name, $"{remoteHost.host}:{remoteHost.port} - Sync done, closing connection");
					remoteHost.scpClient.Disconnect();
				}
			}
		}
		catch (Exception ex)
		{
			Logger.Log(GetType().Name, $"Problem during remote host export: {ex.Message}");
		}
	}
}