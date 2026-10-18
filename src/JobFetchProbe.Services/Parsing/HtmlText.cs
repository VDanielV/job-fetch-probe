using System.Net;
using System.Text.RegularExpressions;

namespace JobFetchProbe.Services.Parsing;

/// <summary>HTML to readable text, shared by both steps (search pages in step 1, job pages in step 2).</summary>
public static partial class HtmlText
{
    /// <summary>Turns HTML into readable text: drops scripts and styles, keeps paragraph and list breaks.</summary>
    public static string ToText(string html)
    {
        var text = NonVisible().Replace(html, " ");
        text = LineBreaks().Replace(text, "\n");
        text = ListItems().Replace(text, "\n- ");
        text = InlineTags().Replace(text, string.Empty);
        text = Tags().Replace(text, " ");
        text = WebUtility.HtmlDecode(text);
        text = Spaces().Replace(text, " ");
        text = SpacesAroundBreaks().Replace(text, "\n");
        text = BlankLines().Replace(text, "\n\n");
        return text.Trim();
    }

    [GeneratedRegex(@"<(script|style|noscript|svg|head)\b.*?</\1\s*>|<!--.*?-->", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex NonVisible();

    [GeneratedRegex(@"<br\s*/?>|</(p|div|h[1-6]|ul|ol|section|article|tr)\s*>", RegexOptions.IgnoreCase)]
    private static partial Regex LineBreaks();

    [GeneratedRegex(@"<li\b[^>]*>", RegexOptions.IgnoreCase)]
    private static partial Regex ListItems();

    // Inline formatting tags are removed without adding a space, so "<strong>Integral</strong>," stays "Integral,".
    [GeneratedRegex(@"</?(strong|b|em|i|u|span|a|small|mark)\b[^>]*>", RegexOptions.IgnoreCase)]
    private static partial Regex InlineTags();

    [GeneratedRegex(@"<[^>]+>")]
    private static partial Regex Tags();

    [GeneratedRegex(@"[ \t\r\f\v]+")]
    private static partial Regex Spaces();

    [GeneratedRegex(@" *\n *")]
    private static partial Regex SpacesAroundBreaks();

    [GeneratedRegex(@"\n{3,}")]
    private static partial Regex BlankLines();
}
