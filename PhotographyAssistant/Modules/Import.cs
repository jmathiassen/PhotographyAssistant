using PhotographyAssistant.Classes;

namespace PhotographyAssistant.Modules;

public class Import(Config config)
{
	public bool NeedsProcessing()
	{
		foreach (string importDirectory in config.import.importDirectories)
			foreach (FileTypeHandling fileType in config.import.fileTypes)
				if (Directory.GetFiles(importDirectory, $"*{fileType.extension}", new EnumerationOptions() { RecurseSubdirectories = true, MatchCasing = MatchCasing.CaseInsensitive }).Length > 0)
					return true;

		return false;
	}
	public void Process()
	{
		try
		{
			string currentDirectory = new DirectoryInfo(".").FullName;
			foreach (string importDirectory in config.import.importDirectories)
			{
				DirectoryInfo directory = new(importDirectory);
				foreach (FileTypeHandling fileType in config.import.fileTypes)
				{
					FileInfo? fileToImport = directory.GetFiles($"*{fileType.extension}", new EnumerationOptions() { RecurseSubdirectories = true, MatchCasing = MatchCasing.CaseInsensitive }).OrderBy(x => x.LastWriteTimeUtc).FirstOrDefault();
					if (fileToImport == null)
						continue;

					string fileToImportPath = fileToImport.FullName.Replace($"{currentDirectory}/", "");
					FileInfo incomingFile = FileOperations.EnsureUniqueFilename(GetType().Name, fileToImport, config.import.incomingDirectory, dateRename: fileType.dateRename);
					if (!incomingFile.Exists)
					{
						Logger.Log(GetType().Name, $"{fileToImportPath} -> {incomingFile}: Copy");
						File.Copy(fileToImport.FullName, incomingFile.FullName);
					}
					if (FileOperations.CompareFile(fileToImport, incomingFile))
					{
						FileInfo promotedFile = FileOperations.EnsureUniqueFilename(GetType().Name, incomingFile, config.import.promoteDirectory);
						if (!promotedFile.Exists)
						{
							Logger.Log(GetType().Name, $"{incomingFile} -> {promotedFile}: Promote");
							File.Move(incomingFile.FullName, promotedFile.FullName);
						}
						Logger.Log(GetType().Name, $"{fileToImportPath}: Delete");
						File.Delete(fileToImport.FullName);
					}
				}
			}
		}
		catch (Exception ex)
		{
			Logger.Log(GetType().Name, $"Problem during import: {ex.Message}, {ex.StackTrace}");
		}
	}
}