using PhotographyAssistant.Classes;

namespace PhotographyAssistant.Modules;

public class TransferExternalHD(Config config)
{
	public bool NeedsProcessing() => config.Access(c => c.external)
		.Where(x => x.active)
		.Any(hd => Directory.GetFiles(hd.promote).Length > 0 && hd.physicalPaths.Where(Directory.Exists)
			.Any());

	public void Process()
	{
		try
		{
			foreach (ExternalHD driveGroup in config.Access(c => c.external).Where(x => x.active))
			{
				string? filePath = Directory.GetFiles(driveGroup.promote).FirstOrDefault();
				if (filePath == null)
					continue;

				FileInfo exportFileFrom = new(Path.Combine(driveGroup.promote, new FileInfo(filePath).Name));
				string? usablePhysicalPath = driveGroup.physicalPaths.Where(path => {
					try { return Directory.Exists(path) && new DriveInfo(path).AvailableFreeSpace > exportFileFrom.Length; }
					catch { return false; }
				}).OrderByDescending(path => {
					try { return new DriveInfo(path).AvailableFreeSpace; }
					catch { return 0; }
				}).FirstOrDefault();

				if (string.IsNullOrWhiteSpace(usablePhysicalPath))
				{
					if (!driveGroup.hasInsufficientSpace)
					{
						Logger.Log(GetType().Name, $"{exportFileFrom.FullName}: No physical path available with enough space");
						driveGroup.hasInsufficientSpace = true;
					}

					continue;
				}

				driveGroup.hasInsufficientSpace = false;

				FileInfo exportFileTo = FileOperations.EnsureUniqueFilename(GetType().Name, exportFileFrom, usablePhysicalPath);
				if (!exportFileTo.Exists)
				{
					Logger.Log(GetType().Name, $"{exportFileFrom.FullName} -> {exportFileTo.FullName}: Copy");
					File.Copy(exportFileFrom.FullName, exportFileTo.FullName);
					if (FileOperations.CompareFile(exportFileFrom.FullName, exportFileTo.FullName))
					{
						Logger.Log(GetType().Name, $"{exportFileFrom.FullName}: File successfully exported");
						File.Delete(exportFileFrom.FullName);
					}
				}
				else if (FileOperations.CompareFile(exportFileFrom.FullName, exportFileTo.FullName))
				{
					Logger.Log(GetType().Name, $"{exportFileFrom.FullName}: File already successfully exported");
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