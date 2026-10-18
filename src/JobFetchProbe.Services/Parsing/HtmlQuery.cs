using System.Net;
using System.Text.RegularExpressions;

namespace JobFetchProbe.Services.Parsing;

/// <summary>
/// Small questions asked of an HTML page with regular expressions: the text of an element, a meta tag, an attribute.
/// Every text comes back through <see cref="HtmlText.ToText"/>, and an empty text is returned as null.
/// </summary>
internal static partial class HtmlQuery
{
    /// <summary>The visible text of some HTML, or null when there is none.</summary>
    public static string? TextOrNull(string html)
    {
        var text = HtmlText.ToText(html);
        if (text.Length == 0)
        {
            return null;
        }
        return text;
    }

    /// <summary>
    /// Text of the first element whose class list contains <paramref name="cssClass"/>. With <paramref name="tag"/>,
    /// only that element name; without it, any element. Inner tags are allowed; the same tag nested inside is not.
    /// </summary>
    public static string? ClassText(string html, string cssClass, string? tag = null)
    {
        var classAttribute = $@"\bclass=""[^""]*\b{Regex.Escape(cssClass)}\b[^""]*""";
        string pattern;
        if (tag is null)
        {
            pattern = $@"<(\w+)\b[^>]*{classAttribute}[^>]*>(?<inner>.*?)</\1\s*>";
        }
        else
        {
            pattern = $@"<{tag}\b[^>]*{classAttribute}[^>]*>(?<inner>.*?)</{tag}\s*>";
        }

        var m = Regex.Match(html, pattern, RegexOptions.IgnoreCase | RegexOptions.Singleline);
        if (!m.Success)
        {
            return null;
        }
        return TextOrNull(m.Groups["inner"].Value);
    }

    /// <summary>Text of the first group 1 of <paramref name="regex"/> in the page.</summary>
    public static string? FirstText(Regex regex, string html)
    {
        var m = regex.Match(html);
        if (!m.Success)
        {
            return null;
        }
        return TextOrNull(m.Groups[1].Value);
    }

    /// <summary>Text of the page's &lt;title&gt;.</summary>
    public static string? Title(string html)
    {
        return FirstText(TitleTag(), html);
    }

    /// <summary>Text of the page's first &lt;h1&gt;.</summary>
    public static string? FirstHeading(string html)
    {
        return FirstText(H1(), html);
    }

    /// <summary>The content of the meta tag whose property or name is <paramref name="name"/> (og:title, description …).</summary>
    public static string? Meta(string html, string name)
    {
        foreach (Match m in MetaTags().Matches(html))
        {
            var tag = m.Value;
            var key = Attribute(tag, "property") ?? Attribute(tag, "name");
            if (!string.Equals(key, name, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var content = Attribute(tag, "content");
            if (string.IsNullOrWhiteSpace(content))
            {
                return null;
            }
            // Decoded twice: some sites double-escape meta content (&amp;amp;).
            return WebUtility.HtmlDecode(WebUtility.HtmlDecode(content)).Trim();
        }
        return null;
    }

    /// <summary>The value of an attribute in one tag, in double or single quotes.</summary>
    public static string? Attribute(string tag, string name)
    {
        var m = Regex.Match(tag, $@"\b{name}\s*=\s*(""([^""]*)""|'([^']*)')", RegexOptions.IgnoreCase);
        if (!m.Success)
        {
            return null;
        }
        if (m.Groups[2].Success)
        {
            return m.Groups[2].Value;
        }
        return m.Groups[3].Value;
    }

    [GeneratedRegex(@"<meta\b[^>]*>", RegexOptions.IgnoreCase)]
    private static partial Regex MetaTags();

    [GeneratedRegex(@"<h1\b[^>]*>(.*?)</h1>", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex H1();

    [GeneratedRegex(@"<title\b[^>]*>(.*?)</title>", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex TitleTag();
}
