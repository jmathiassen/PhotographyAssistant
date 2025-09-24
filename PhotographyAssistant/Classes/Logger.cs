namespace PhotographyAssistant.Classes;

public class Logger
{
    public static void Log(string category, string log)
    {
        Console.WriteLine($"[{timeFormat(DateTime.Now)}] [{category}]: {log}");
    }
	private static string timeFormat(DateTime date) => $"{date.Year:0000}-{date.Month:00}-{date.Day:00} {date.Hour:00}:{date.Minute:00}:{date.Second:00}.{date.Millisecond:000}";
}
