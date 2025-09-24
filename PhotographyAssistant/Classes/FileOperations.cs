using MetadataExtractor.Formats.Exif;
using MetadataExtractor;

namespace PhotographyAssistant.Classes;

public class FileOperations
{
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
				DateTime fileDate = sourceFile.LastWriteTime;
				try
				{
					fileDate = ImageMetadataReader.ReadMetadata(sourceFile.FullName).OfType<ExifSubIfdDirectory>().Last().GetDateTime(ExifDirectoryBase.TagDateTimeOriginal);
				}
				catch (Exception e)
				{
					Logger.Log(context, $"{sourceFile.FullName} - image processing exception {e.Message} while reading exif -- fallback to last write time");
				}

				string prefix = $"{fileDate.Year:0000}-{fileDate.Month:00}-{fileDate.Day:00} {fileDate.Hour:00}-{fileDate.Minute:00}-{fileDate.Second:00}";
				filenameTo = keepOriginalFilename ? $"{prefix}+{filenameTo}" : prefix;
			}
			destinationFile = new FileInfo(Path.Combine(destinationPath, $"{Path.GetFileNameWithoutExtension(filenameTo)}{postfix}{sourceFile.Extension}"));
			if (!destinationFile.Exists || CompareFile(sourceFile.FullName, destinationFile.FullName))
				break;

			Logger.Log(context, $"{sourceFile} -> {destinationFile} File exist with different content, continue looking");
			postfix = $" ({++retries})";
		}
		while (destinationFile.Exists);
		return destinationFile;
	}
	public static bool CompareFile(string fromPath, string toPath)
	{
		FileInfo from = new(fromPath);
		FileInfo to = new(toPath);

		if (from.Length != to.Length)
			return false;

		if (from.Length == 0)
			return true;

		const int bufferSize = 8192;
		byte[] buffer1 = new byte[bufferSize];
		byte[] buffer2 = new byte[bufferSize];

		try
		{
			using FileStream streamFrom = from.OpenRead();
			using FileStream streamTo = to.OpenRead();

			while (true)
			{
				int bytesReadFrom = streamFrom.Read(buffer1, 0, bufferSize);
				int bytesReadTo = streamTo.Read(buffer2, 0, bufferSize);

				if (bytesReadFrom != bytesReadTo)
					return false;

				if (bytesReadFrom <= 0)
					return true;

				if (!buffer1.AsSpan(0, bytesReadFrom).SequenceEqual(buffer2.AsSpan(0, bytesReadTo)))
					return false;
			}
		}
		catch (Exception e)
		{
			Logger.Log("CompareFile", $"Problem during file compare: {e.Message}");
			throw;
		}
	}
}
