using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace JobFetchProbe.Services.Parsing;

/// <summary>
/// schema.org data that pages embed for search engines (&lt;script type="application/ld+json"&gt;): finding an object
/// of a given type, in arrays and @graph lists too, and reading strings from it.
/// </summary>
internal static partial class JsonLd
{
    /// <summary>
    /// Finds the first object of <paramref name="type"/> (for example "JobPosting") in the page's JSON-LD blocks and
    /// reads it with <paramref name="read"/>. Blocks that are not valid JSON are skipped. Null when there is none.
    /// </summary>
    public static T? ReadFirst<T>(string html, string type, Func<JsonElement, T> read) where T : class
    {
        foreach (Match m in Blocks().Matches(html))
        {
            JsonDocument doc;
            try
            {
                doc = JsonDocument.Parse(WebUtility.HtmlDecode(m.Groups[1].Value).Trim());
            }
            catch (JsonException)
            {
                continue;
            }

            using (doc)
            {
                if (Find(doc.RootElement, type) is { } found)
                {
                    return read(found);
                }
            }
        }
        return null;
    }

    /// <summary>Reads a string at a property path. Numbers come back as written; arrays of strings are joined with ", ".</summary>
    public static string? String(JsonElement e, params string[] path)
    {
        foreach (var name in path)
        {
            if (e.ValueKind != JsonValueKind.Object || !e.TryGetProperty(name, out e))
            {
                return null;
            }
        }

        switch (e.ValueKind)
        {
            case JsonValueKind.String:
                return e.GetString()?.Trim();
            case JsonValueKind.Number:
                return e.GetRawText();
            case JsonValueKind.Array:
                return string.Join(", ", e.EnumerateArray()
                    .Where(x => x.ValueKind == JsonValueKind.String)
                    .Select(x => x.GetString()));
            default:
                return null;
        }
    }

    private static JsonElement? Find(JsonElement e, string type)
    {
        switch (e.ValueKind)
        {
            case JsonValueKind.Array:
                foreach (var item in e.EnumerateArray())
                {
                    if (Find(item, type) is { } found)
                    {
                        return found;
                    }
                }
                break;

            case JsonValueKind.Object:
                if (e.TryGetProperty("@type", out var typeValue) && IsType(typeValue, type))
                {
                    return e;
                }
                if (e.TryGetProperty("@graph", out var graph))
                {
                    return Find(graph, type);
                }
                break;
        }
        return null;
    }

    /// <summary>"@type" is a string or a list of strings.</summary>
    private static bool IsType(JsonElement value, string type)
    {
        switch (value.ValueKind)
        {
            case JsonValueKind.String:
                return value.GetString() == type;
            case JsonValueKind.Array:
                return value.EnumerateArray().Any(t => t.ValueKind == JsonValueKind.String && t.GetString() == type);
            default:
                return false;
        }
    }

    [GeneratedRegex(@"<script[^>]*type\s*=\s*[""']application/ld\+json[""'][^>]*>(.*?)</script>", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex Blocks();
}
