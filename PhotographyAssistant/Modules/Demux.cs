using PhotographyAssistant.Classes;

namespace PhotographyAssistant.Modules;

public class Demux(Config config)
{
	public bool NeedsProcessing()
	{
		if (Directory.GetFiles(config.import.promoteDirectory).Length > 0)
			return true;

		return false;
	}
	public void Process()
	{
		int exports = 0;
		try
		{
			exports += config.external.Where(x => x.active).Count();
			exports += config.remote.Where(x => x.active).Count();

			string? filePath = Directory.GetFiles(config.import.promoteDirectory).FirstOrDefault();
			if (filePath == null)
				return;

			FileInfo fileToDemux = new($"{config.import.promoteDirectory}/{new FileInfo(filePath).Name}");
			List<FileInfo> demuxedPaths = [];
			foreach (ExternalHD drive in config.external.Where(x => x.active))
			{
				FileInfo incomingFile = FileOperations.EnsureUniqueFilename(GetType().Name, fileToDemux, drive.incoming);
				if (!incomingFile.Exists)
				{
					Logger.Log(GetType().Name, $"{fileToDemux} -> {incomingFile}: Copy");
					File.Copy(fileToDemux.FullName, incomingFile.FullName);
				}
				if (FileOperations.CompareFile(fileToDemux, incomingFile))
				{
					FileInfo promotedFile = FileOperations.EnsureUniqueFilename(GetType().Name, incomingFile, drive.promote);
					if (!promotedFile.Exists)
					{
						Logger.Log(GetType().Name, $"{incomingFile} -> {promotedFile}: Promote");
						File.Move(incomingFile.FullName, promotedFile.FullName);
					}
					demuxedPaths.Add(promotedFile);
				}
			}

			foreach (RemoteHost host in config.remote.Where(x => x.active))
			{
				var incomingFile = FileOperations.EnsureUniqueFilename(GetType().Name, fileToDemux, host.incoming);
				if (!incomingFile.Exists)
				{
					Logger.Log(GetType().Name, $"{fileToDemux} -> {incomingFile}: Copy");
					File.Copy(fileToDemux.FullName, incomingFile.FullName);
				}
				else
				{
					Logger.Log(GetType().Name, $"{fileToDemux} -> {incomingFile}: File copied");
				}
				if (FileOperations.CompareFile(fileToDemux, incomingFile))
				{
					FileInfo promotedFile = FileOperations.EnsureUniqueFilename(GetType().Name, incomingFile, host.promote);
					if (!promotedFile.Exists)
					{
						Logger.Log(GetType().Name, $"{incomingFile} -> {promotedFile}: Promote");
						File.Move(incomingFile.FullName, promotedFile.FullName);
					}
					demuxedPaths.Add(promotedFile);
				}
			}

			bool finished = true;
			foreach (FileInfo demuxedFile in demuxedPaths)
				if (!FileOperations.CompareFile(fileToDemux, demuxedFile))
					finished = false;

			if (finished)
			{
				File.Delete(fileToDemux.FullName);
				Logger.Log(GetType().Name, $"{fileToDemux}: Delete");
			}
		}
		catch (Exception ex)
		{
			Logger.Log(GetType().Name, $"Problem during demux: {ex.Message} {ex.StackTrace}");
		}
	}
}