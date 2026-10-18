using System.Globalization;
using System.Net;
using System.Text;
using Microsoft.AspNetCore.Html;

namespace JobFetchProbe.Web.Pages;

/// <summary>
/// Marks the text filter's terms in a text (&lt;mark&gt;), ignoring capitals and accents as the filter does. The text is
/// HTML-encoded; only the marks are markup.
/// </summary>
public static class Highlight
{
    public static IHtmlContent Html(string? text, IReadOnlyList<string>? terms)
    {
        if (string.IsNullOrEmpty(text))
        {
            return HtmlString.Empty;
        }
        var spans = Find(text, terms);
        if (spans.Count == 0)
        {
            return new HtmlString(WebUtility.HtmlEncode(text));
        }

        var html = new StringBuilder();
        var at = 0;
        foreach (var (start, length) in spans)
        {
            html.Append(WebUtility.HtmlEncode(text[at..start]));
            html.Append("<mark>").Append(WebUtility.HtmlEncode(text.Substring(start, length))).Append("</mark>");
            at = start + length;
        }
        html.Append(WebUtility.HtmlEncode(text[at..]));
        return new HtmlString(html.ToString());
    }

    /// <summary>How many times the terms appear in the text.</summary>
    public static int Count(string? text, IReadOnlyList<string>? terms)
    {
        if (string.IsNullOrEmpty(text))
        {
            return 0;
        }
        return Find(text, terms).Count;
    }

    /// <summary>Where the terms are, in order, without overlaps (the longer term wins at the same place).</summary>
    private static List<(int Start, int Length)> Find(string text, IReadOnlyList<string>? terms)
    {
        var spans = new List<(int Start, int Length)>();
        if (terms is null || terms.Count == 0)
        {
            return spans;
        }

        var folded = Fold(text);
        foreach (var term in terms.Select(Fold).Where(t => t.Length > 0).Distinct())
        {
            var at = folded.IndexOf(term, StringComparison.Ordinal);
            while (at >= 0)
            {
                spans.Add((at, term.Length));
                at = folded.IndexOf(term, at + term.Length, StringComparison.Ordinal);
            }
        }

        var result = new List<(int Start, int Length)>();
        foreach (var span in spans.OrderBy(s => s.Start).ThenByDescending(s => s.Length))
        {
            if (result.Count == 0 || span.Start >= result[^1].Start + result[^1].Length)
            {
                result.Add(span);
            }
        }
        return result;
    }

    /// <summary>Lower case without accents, one character per character, as the API's filter compares text.</summary>
    private static string Fold(string text)
    {
        var result = new StringBuilder(text.Length);
        foreach (var c in text)
        {
            if (c < 128)
            {
                result.Append(char.ToLowerInvariant(c));
                continue;
            }
            if (char.IsSurrogate(c))
            {
                result.Append(c); // half of an emoji: kept as is
                continue;
            }
            var folded = char.ToLowerInvariant(c);
            foreach (var d in c.ToString().Normalize(NormalizationForm.FormD))
            {
                if (CharUnicodeInfo.GetUnicodeCategory(d) != UnicodeCategory.NonSpacingMark)
                {
                    folded = char.ToLowerInvariant(d);
                    break;
                }
            }
            result.Append(folded);
        }
        return result.ToString();
    }
}
