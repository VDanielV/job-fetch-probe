using System.Text.RegularExpressions;

namespace JobFetchProbe.Services.Fit;

/// <summary>
/// A mandatory line that requires Java, Python, PHP, Node.js, React, or Go, and does not accept C# or .NET on that
/// same line, is a hard skip. "C# and/or Go" stays open because C# is production experience.
/// </summary>
internal static partial class HardSkipStack
{
    public static bool Required(IEnumerable<string> mandatoryRequirements)
    {
        foreach (var line in mandatoryRequirements)
        {
            if (Blocked().IsMatch(line) && !ProductionAlternative().IsMatch(line))
            {
                return true;
            }
        }

        return false;
    }

    [GeneratedRegex(@"Java(?!Script)|\bPython\b|\bPHP\b|Node(?:\.js)?|\bReact\b|\bGo\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex Blocked();

    [GeneratedRegex(@"C#|\.NET|ASP\.NET", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex ProductionAlternative();
}
