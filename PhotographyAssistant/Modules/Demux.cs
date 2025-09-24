using PhotographyAssistant.Classes;

namespace PhotographyAssistant.Modules;

public class Demux(Config config)
{
	public bool NeedsProcessing() => Directory.GetFiles(config.Access(c => c.import).promoteDirectory).Length > 0;

	public void Process()
	{
		try
		{
			string? filePath = Directory.GetFiles(config.Access(c => c.import).promoteDirectory).FirstOrDefault();
			if (filePath == null)
				return;

			FileInfo fileToDemux = new(Path.Combine(config.Access(c => c.import).promoteDirectory, new FileInfo(filePath).Name));
			List<FileInfo> demuxedPaths = [];
			foreach (ExternalHD drive in config.Access(c => c.external).Where(externalHd => externalHd.active))
			{
				FileInfo incomingFile = FileOperations.EnsureUniqueFilename(GetType().Name, fileToDemux, drive.incoming);
				if (!incomingFile.Exists)
				{
					Logger.Log(GetType().Name, $"{fileToDemux.FullName} -> {incomingFile.FullName}: Copy");
					File.Copy(fileToDemux.FullName, incomingFile.FullName);
				}
				if (FileOperations.CompareFile(fileToDemux.FullName, incomingFile.FullName))
				{
					FileInfo promotedFile = FileOperations.EnsureUniqueFilename(GetType().Name, incomingFile, drive.promote);
					if (!promotedFile.Exists)
					{
						Logger.Log(GetType().Name, $"{incomingFile.FullName} -> {promotedFile.FullName}: Promote");
						File.Move(incomingFile.FullName, promotedFile.FullName);
					}
					else
						Logger.Log(GetType().Name, $"{fileToDemux.FullName} -> {incomingFile.FullName}: External HD file is the same, not promoting");
					demuxedPaths.Add(promotedFile);
				}
				else
					Logger.Log(GetType().Name, $"{fileToDemux.FullName} -> {incomingFile.FullName}: External HD file is the same, not copied");
			}

			foreach (RemoteHost host in config.Access(c => c.remote).Where(remoteHost => remoteHost.active))
			{
				FileInfo incomingFile = FileOperations.EnsureUniqueFilename(GetType().Name, fileToDemux, host.incoming);
				if (!incomingFile.Exists)
				{
					Logger.Log(GetType().Name, $"{fileToDemux.FullName} -> {incomingFile.FullName}: Copy");
					File.Copy(fileToDemux.FullName, incomingFile.FullName);
				}
				else
					Logger.Log(GetType().Name, $"{fileToDemux.FullName} -> {incomingFile.FullName}: File copied");

				if (FileOperations.CompareFile(fileToDemux.FullName, incomingFile.FullName))
				{
					FileInfo promotedFile = FileOperations.EnsureUniqueFilename(GetType().Name, incomingFile, host.promote);
					if (!promotedFile.Exists)
					{
						Logger.Log(GetType().Name, $"{incomingFile.FullName} -> {promotedFile.FullName}: Promote");
						File.Move(incomingFile.FullName, promotedFile.FullName);
					}
					else
						Logger.Log(GetType().Name, $"{fileToDemux.FullName} -> {incomingFile.FullName}: Remote file is the same, not promoting");
					demuxedPaths.Add(promotedFile);
				}
				else
					Logger.Log(GetType().Name, $"{fileToDemux.FullName} -> {incomingFile.FullName}: Remote file is the same, not copied");
			}

			bool finished = true;
			foreach (FileInfo demuxedFile in demuxedPaths)
				if (!FileOperations.CompareFile(fileToDemux.FullName, demuxedFile.FullName))
					finished = false;

			if (finished)
			{
				File.Delete(fileToDemux.FullName);
				Logger.Log(GetType().Name, $"{fileToDemux.FullName}: Delete");
			}
		}
		catch (Exception ex)
		{
			Logger.Log(GetType().Name, $"Problem during demux: {ex.Message} {ex.StackTrace}");
		}
	}
}