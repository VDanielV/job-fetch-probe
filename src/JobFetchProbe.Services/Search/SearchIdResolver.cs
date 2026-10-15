using System.Globalization;
using System.Text;

namespace JobFetchProbe.Services.Search;

/// <summary>
/// Names a search after its address, so the same search always writes to the same file:
/// /jobs/%2Enet-empleos-valencia               → net-empleos-valencia
/// /jobs/search?keywords=.net&amp;location=Valencia  → net-valencia
/// </summary>
public static class SearchIdResolver
{
    public static string? FromUrl(Uri url)
    {
        string raw;
        if (url.AbsolutePath.TrimEnd('/').EndsWith("/jobs/search", StringComparison.OrdinalIgnoreCase))
        {
            var query = ParseQuery(url.Query);
            raw = string.Join("-", new[] { "keywords", "location" }
                .Select(k => query.GetValueOrDefault(k))
                .Where(v => !string.IsNullOrWhiteSpace(v)));
        }
        else
        {
            raw = Uri.UnescapeDataString(url.AbsolutePath.TrimEnd('/').Split('/').LastOrDefault() ?? string.Empty);
        }

        var slug = Slug(raw);
        return slug.Length > 0 ? slug : null;
    }

    /// <summary>Lower case, accents removed, anything other than a-z and 0-9 becomes a single "-".</summary>
    public static string Slug(string text)
    {
        var sb = new StringBuilder();
        foreach (var c in text.Normalize(NormalizationForm.FormD))
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }
            var lower = char.ToLowerInvariant(c);
            if (lower is >= 'a' and <= 'z' or >= '0' and <= '9')
            {
                sb.Append(lower);
            }
            else if (sb.Length > 0 && sb[^1] != '-')
            {
                sb.Append('-');
            }
        }
        return sb.ToString().Trim('-');
    }

    private static Dictionary<string, string> ParseQuery(string query)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var pair in query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var parts = pair.Split('=', 2);
            var key = Uri.UnescapeDataString(parts[0].Replace('+', ' '));
            var value = parts.Length > 1 ? Uri.UnescapeDataString(parts[1].Replace('+', ' ')) : string.Empty;
            result.TryAdd(key, value);
        }
        return result;
    }
}
