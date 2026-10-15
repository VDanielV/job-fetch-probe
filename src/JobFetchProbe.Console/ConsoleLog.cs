namespace JobFetchProbe.Console;

/// <summary>Timestamped console lines, so a long run shows when each thing happened.</summary>
public static class ConsoleLog
{
    public static void Info(string message) => Write(message, null);

    public static void Good(string message) => Write(message, ConsoleColor.Green);

    public static void Warn(string message) => Write(message, ConsoleColor.Yellow);

    public static void Bad(string message) => Write(message, ConsoleColor.Red);

    /// <summary>A heading line without a timestamp.</summary>
    public static void Heading(string title)
    {
        System.Console.WriteLine();
        System.Console.WriteLine($"===== {title} =====");
    }

    public static string Duration(TimeSpan t) =>
        t.TotalHours >= 1 ? $"{(int)t.TotalHours}h {t.Minutes:00}m"
        : t.TotalMinutes >= 1 ? $"{t.Minutes}m {t.Seconds:00}s"
        : $"{Math.Max(0, (int)Math.Round(t.TotalSeconds))}s";

    private static void Write(string message, ConsoleColor? color)
    {
        System.Console.Write($"[{DateTime.Now:HH:mm:ss}] ");
        if (color is { } c && !System.Console.IsOutputRedirected)
        {
            var previous = System.Console.ForegroundColor;
            System.Console.ForegroundColor = c;
            System.Console.WriteLine(message);
            System.Console.ForegroundColor = previous;
        }
        else
        {
            System.Console.WriteLine(message);
        }
    }
}
