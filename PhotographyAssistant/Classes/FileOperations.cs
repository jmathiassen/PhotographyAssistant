using MetadataExtractor.Formats.Exif;
using MetadataExtractor;

namespace PhotographyAssistant.Classes;

public class FileOperations
{
	public static string? FindFile(string currentDirectoryPath, string[]? filter = null)
	{
		DirectoryInfo currentDirectory = new(currentDirectoryPath);
		foreach (DirectoryInfo directory in currentDirectory.GetDirectories())
		{
			string? file = FindFile($"{currentDirectoryPath}/{directory.Name}", filter);
			if (file != null)
				return file;
		}

		foreach (FileInfo file in currentDirectory.GetFiles())
		{
			if (filter != null && !filter.Contains(file.Extension.ToLower()))
				continue;

			return $"{currentDirectoryPath}/{file.Name}";
		}

		return null;
	}
	public static FileInfo EnsureUniqueFilename(string context, FileInfo sourceFile, string destinationPath, bool dateRename = false, bool keepOriginalFilename = true)
	{
		int retries = 0;
		string postfix = "";

		FileInfo destinationFile;
		do
		{
			string filenameTo = sourceFile.Name;
			if (dateRename)
			{
				string prefix;
				DateTime fileDate;
				try
				{
					fileDate = ImageMetadataReader.ReadMetadata(sourceFile.FullName).OfType<ExifSubIfdDirectory>().Last().GetDateTime(ExifDirectoryBase.TagDateTimeOriginal);
				}
				catch
				{
					Logger.Log(context, $"{sourceFile.FullName} - exif read failed, fallback to last write time");
					fileDate = sourceFile.LastWriteTime;
				}
				prefix = $"{fileDate.Year}-{fileDate.Month.ToString().PadLeft(2, '0')}-{fileDate.Day.ToString().PadLeft(2, '0')} {fileDate.Hour.ToString().PadLeft(2, '0')}-{fileDate.Minute.ToString().PadLeft(2, '0')}-{fileDate.Second.ToString().PadLeft(2, '0')}";
				if (keepOriginalFilename)
					filenameTo = $"{prefix}+{filenameTo}";
				else
					filenameTo = prefix;
			}
			destinationFile = new($"{destinationPath}/{filenameTo.Replace(sourceFile.Extension, "")}{postfix}{sourceFile.Extension}");
			if (!destinationFile.Exists || CompareFile(sourceFile, destinationFile))
				break;

			Logger.Log(context, $"{sourceFile} -> {destinationFile} File exist with different content, continue looking");
			postfix = $" ({++retries})";
		}
		while (destinationFile.Exists);
		return destinationFile;
	}
	public static bool CompareFile(FileInfo from, FileInfo to, int bufferSize = sizeof(Int64) * 1024)
	{
		byte[] buffer1 = new byte[bufferSize];
		byte[] buffer2 = new byte[bufferSize];

		using var streamFrom = from.OpenRead();
		using var streamTo = to.OpenRead();

		int count1 = streamFrom.Read(buffer1, 0, bufferSize);
		int count2 = streamTo.Read(buffer2, 0, bufferSize);

		if (count1 != count2)
			return false;

		if (count1 > 0)
		{
			int iterations = (int)Math.Ceiling((double)count1 / sizeof(Int64));
			for (int i = 0; i < iterations; i++)
			{
				if (BitConverter.ToInt64(buffer1, i * sizeof(Int64)) != BitConverter.ToInt64(buffer2, i * sizeof(Int64)))
					return false;
			}
		}

		return true;
	}
}
