using PhotographyAssistant.Classes;

namespace PhotographyAssistant.Modules;

public class Import(Config config)
{
	private HashSet<string> ExternalHardDiskPaths = [];
	private HashSet<string> ProcessedSDCards = [];
	private HashSet<string> NeedsProcessingDirectories = [];

	public void CalculateProcessing()
	{
		foreach (string disconnectedHardDisk in ExternalHardDiskPaths.Where(path => !Directory.Exists(path)))
		{
			Logger.Log(GetType().Name, $"{disconnectedHardDisk}: Removing external HD directory exclusion after disconnect");
			ExternalHardDiskPaths.Remove(disconnectedHardDisk);
		}

		foreach (string disconnectedSDCard in ProcessedSDCards.Where(x => !Directory.Exists(x)))
		{
			Logger.Log(GetType().Name, $"{disconnectedSDCard}: Removing processed SD directory after disconnect");
			ProcessedSDCards.Remove(disconnectedSDCard);
		}

		NeedsProcessingDirectories.Clear();

		foreach (DirectoryInfo importRootDirectory in config.Access(c => c.import.importDirectories.Select(x => new DirectoryInfo(x))))
		{
			if (!importRootDirectory.Exists)
				continue;

			foreach (DirectoryInfo directoryToCheck in importRootDirectory.GetDirectories())
			{
				if (ExternalHardDiskPaths.Contains(directoryToCheck.FullName) || ProcessedSDCards.Contains(directoryToCheck.FullName))
					continue;

				if (File.Exists(Path.Combine(directoryToCheck.FullName, config.Access(c => c.import.externalHardDiskIdentifier))))
				{
					Logger.Log(GetType().Name, $"{directoryToCheck.FullName}: Adding external HD directory exclusion");
					ExternalHardDiskPaths.Add(directoryToCheck.FullName);
				}
				else
				{
					Logger.Log(GetType().Name, $"{directoryToCheck.FullName}: Added to processing list");
					NeedsProcessingDirectories.Add(directoryToCheck.FullName);
				}
			}
		}
	}
	public bool NeedsProcessing() => NeedsProcessingDirectories.Any();

	public void Process()
	{
		try
		{
			foreach (DirectoryInfo directory in NeedsProcessingDirectories.Select(path => new DirectoryInfo(path)).ToList())
			{
				Logger.Log(GetType().Name, $"{directory.FullName}: Perform import processing");
				foreach (FileTypeHandling fileType in config.Access(c => c.import.fileTypes))
				{
					foreach (FileInfo fileToImport in directory.GetFiles($"*{fileType.extension}", new EnumerationOptions { RecurseSubdirectories = true, MatchCasing = MatchCasing.CaseInsensitive }).OrderBy(x => x.LastWriteTimeUtc))
					{
						Logger.Log(GetType().Name, $"{fileToImport}: Checking for import");
						if (ExternalHardDiskPaths.Any(x => fileToImport.FullName.StartsWith(x)) ||
							ProcessedSDCards.Any(x => fileToImport.FullName.StartsWith(x)) ||
							!NeedsProcessingDirectories.Any(x => fileToImport.FullName.StartsWith(x)))
						{
							Logger.Log(GetType().Name, $"{fileToImport.FullName}: Skipping file as it is in an excluded directory");
							continue;
						}

						FileInfo incomingFile = FileOperations.EnsureUniqueFilename(GetType().Name, fileToImport, config.Access(c => c.import.incomingDirectory), dateRename: fileType.dateRename);
						if (!incomingFile.Exists)
						{
							Logger.Log(GetType().Name, $"{fileToImport.FullName} -> {incomingFile.FullName}: Copy");
							File.Copy(fileToImport.FullName, incomingFile.FullName);
						}

						if (FileOperations.CompareFile(fileToImport.FullName, incomingFile.FullName))
						{
							FileInfo promotedFile = FileOperations.EnsureUniqueFilename(GetType().Name, incomingFile, config.Access(c => c.import.promoteDirectory));
							if (!promotedFile.Exists)
							{
								Logger.Log(GetType().Name, $"{incomingFile.FullName} -> {promotedFile.FullName}: Promote");
								File.Move(incomingFile.FullName, promotedFile.FullName);
							}
							else
								Logger.Log(GetType().Name, $"{incomingFile.FullName} -> {promotedFile.FullName}: Imported file is the same, not promoting");
							Logger.Log(GetType().Name, $"{fileToImport.FullName}: Delete");
							File.Delete(fileToImport.FullName);
						}
					}
				}

				Logger.Log(GetType().Name, $"{directory.FullName}: Card import completed");
				ProcessedSDCards.Add(directory.FullName);
				NeedsProcessingDirectories.Remove(directory.FullName);
			}
		}
		catch (Exception ex)
		{
			Logger.Log(GetType().Name, $"Problem during import: {ex.Message}, {ex.StackTrace}");
		}
	}
}