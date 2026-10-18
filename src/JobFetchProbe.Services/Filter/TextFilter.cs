using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace JobFetchProbe.Services.Filter;

/// <summary>
/// The Overview's text filter: reads the query (words; -word to exclude; "a phrase"), and tells whether a job's text
/// matches, ignoring capitals and accents. Pure functions; a term also matches inside a longer word.
/// </summary>
internal static partial class TextFilter
{
    private const int SnippetBefore = 60;
    private const int SnippetAfter = 80;

    /// <summary>The query's terms. <see cref="TextFilterQuery.IsEmpty"/> when there is nothing to look for.</summary>
    public static TextFilterQuery Parse(string? text)
    {
        var include = new List<string>();
        var exclude = new List<string>();
        if (string.IsNullOrWhiteSpace(text))
        {
            return new TextFilterQuery(include, exclude);
        }

        foreach (Match m in Terms().Matches(text))
        {
            var term = m.Value;
            var excluded = term.StartsWith('-') && term.Length > 1;
            if (excluded)
            {
                term = term[1..];
            }
            term = term.Trim('"').Trim();
            if (term.Length == 0)
            {
                continue;
            }
            if (excluded)
            {
                exclude.Add(term);
            }
            else
            {
                include.Add(term);
            }
        }
        return new TextFilterQuery(include, exclude);
    }

    /// <summary>True when every included term is in the text and no excluded one is.</summary>
    public static bool Matches(TextFilterQuery query, string? title, string? description, bool searchDescription)
    {
        var text = Fold(title);
        if (searchDescription)
        {
            text += "\n" + Fold(description);
        }
        foreach (var term in query.Include)
        {
            if (!text.Contains(Fold(term), StringComparison.Ordinal))
            {
                return false;
            }
        }
        foreach (var term in query.Exclude)
        {
            if (text.Contains(Fold(term), StringComparison.Ordinal))
            {
                return false;
            }
        }
        return true;
    }

    /// <summary>
    /// When the title holds none of the included terms: about 140 characters of the description around the first one
    /// found, cut at spaces, with "…" where it was cut. Null when the title matched or the description has none.
    /// </summary>
    public static string? Snippet(TextFilterQuery query, string? title, string? description)
    {
        if (string.IsNullOrWhiteSpace(description) || query.Include.Count == 0)
        {
            return null;
        }
        var foldedTitle = Fold(title);
        if (query.Include.Any(t => foldedTitle.Contains(Fold(t), StringComparison.Ordinal)))
        {
            return null;
        }

        var folded = Fold(description);
        var first = -1;
        foreach (var term in query.Include)
        {
            var at = folded.IndexOf(Fold(term), StringComparison.Ordinal);
            if (at >= 0 && (first < 0 || at < first))
            {
                first = at;
            }
        }
        if (first < 0)
        {
            return null;
        }

        var start = Math.Max(0, first - SnippetBefore);
        var end = Math.Min(description.Length, first + SnippetAfter);
        if (start > 0)
        {
            var space = description.IndexOf(' ', start);
            if (space >= 0 && space < first)
            {
                start = space + 1;
            }
        }
        if (end < description.Length)
        {
            var space = description.LastIndexOf(' ', end);
            if (space > first)
            {
                end = space;
            }
        }

        var snippet = Regex.Replace(description[start..end], @"\s+", " ").Trim();
        if (start > 0)
        {
            snippet = "…" + snippet;
        }
        if (end < description.Length)
        {
            snippet += "…";
        }
        return snippet;
    }

    /// <summary>
    /// Lower case without accents, one character for each character of the text (so positions stay the same):
    /// "Técnico" → "tecnico".
    /// </summary>
    public static string Fold(string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return string.Empty;
        }
        var result = new StringBuilder(text.Length);
        foreach (var c in text)
        {
            result.Append(FoldChar(c));
        }
        return result.ToString();
    }

    private static char FoldChar(char c)
    {
        if (c < 128)
        {
            return char.ToLowerInvariant(c);
        }
        if (char.IsSurrogate(c))
        {
            return c; // half of an emoji or another character outside the basic plane: kept as is
        }
        var decomposed = c.ToString().Normalize(NormalizationForm.FormD);
        foreach (var d in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(d) != UnicodeCategory.NonSpacingMark)
            {
                return char.ToLowerInvariant(d);
            }
        }
        return char.ToLowerInvariant(c);
    }

    /// <summary>A quoted phrase (optionally with a minus in front), or a run of non-spaces.</summary>
    [GeneratedRegex("-?\"[^\"]*\"?|\\S+")]
    private static partial Regex Terms();
}
