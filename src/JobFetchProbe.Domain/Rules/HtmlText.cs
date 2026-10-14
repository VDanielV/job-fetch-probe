using System.Net;
using System.Text.RegularExpressions;

namespace JobFetchProbe.Domain.Rules;

/// <summary>Visible text: script, style and comments removed, tags removed, entities decoded, whitespace collapsed.</summary>
public static partial class HtmlText
{
    public static string Visible(string html)
    {
        if (string.IsNullOrEmpty(html))
        {
            return string.Empty;
        }

        var text = NonVisibleBlocks().Replace(html, " ");
        text = Tags().Replace(text, " ");
        text = WebUtility.HtmlDecode(text);
        return Whitespace().Replace(text, " ").Trim();
    }

    public static bool HasH1(string html) =>
        !string.IsNullOrEmpty(html) && H1().IsMatch(NonVisibleBlocks().Replace(html, " "));

    public static string CollapseWhitespace(string text) => Whitespace().Replace(text, " ").Trim();

    [GeneratedRegex(@"<script\b.*?</script\s*>|<style\b.*?</style\s*>|<!--.*?-->",
        RegexOptions.IgnoreCase | RegexOptions.Singleline, matchTimeoutMilliseconds: 2000)]
    private static partial Regex NonVisibleBlocks();

    [GeneratedRegex(@"<[^>]*>", RegexOptions.Singleline, matchTimeoutMilliseconds: 2000)]
    private static partial Regex Tags();

    [GeneratedRegex(@"\s+", RegexOptions.None, matchTimeoutMilliseconds: 2000)]
    private static partial Regex Whitespace();

    [GeneratedRegex(@"<h1[\s>/]", RegexOptions.IgnoreCase, matchTimeoutMilliseconds: 2000)]
    private static partial Regex H1();
}
