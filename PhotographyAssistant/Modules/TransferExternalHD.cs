using PhotographyAssistant.Classes;

namespace PhotographyAssistant.Modules;

public class TransferExternalHD(Config config)
{
	public bool NeedsProcessing()
	{
		foreach (ExternalHD hd in config.external.Where(x => x.active))
		{
			if (Directory.GetFiles(hd.promote).Length > 0 && Directory.Exists(hd.storage))
				return true;
		}

		return false;
	}
	public void Process()
	{
		try
		{
			foreach (var directory in config.external.Where(x => x.active))
			{
				if (!Directory.Exists(directory.storage))
					continue;

				string? filePath = Directory.GetFiles(directory.promote).FirstOrDefault();
				if (filePath == null)
					continue;

				FileInfo exportFileFrom = new($"{directory.promote}/{new FileInfo(filePath).Name}");
				FileInfo exportFileTo = FileOperations.EnsureUniqueFilename(GetType().Name, exportFileFrom, directory.storage);
				if (!exportFileTo.Exists)
				{
					Logger.Log(GetType().Name, $"{exportFileFrom} -> {exportFileTo}: Copy");
					File.Copy(exportFileFrom.FullName, exportFileTo.FullName);
					if (FileOperations.CompareFile(exportFileFrom, exportFileTo))
					{
						Logger.Log(GetType().Name, $"{exportFileFrom}: File successfully exported");
						File.Delete(exportFileFrom.FullName);
					}
				}
				else if (FileOperations.CompareFile(exportFileFrom, exportFileTo))
				{
					Logger.Log(GetType().Name, $"{exportFileFrom}: File already successfully exported");
					File.Delete(exportFileFrom.FullName);
				}
			}
		}
		catch (Exception ex)
		{
			Logger.Log(GetType().Name, $"Problem during external HD export: {ex.Message}");
		}
	}
}