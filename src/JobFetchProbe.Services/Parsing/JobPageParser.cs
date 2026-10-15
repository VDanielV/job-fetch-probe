using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using JobFetchProbe.Models;

namespace JobFetchProbe.Services.Parsing;

/// <summary>
/// Pulls the job out of a page, trying three layouts in order:
/// 1. schema.org JobPosting data embedded for search engines (&lt;script type="application/ld+json"&gt;);
/// 2. LinkedIn's public (guest) job page, identified by its top-card and description classes;
/// 3. generic fallback: og: meta tags, the page title and the visible text.
/// </summary>
public sealed partial class JobPageParser : IJobPageParser
{
    public JobPosting Parse(string html, string jobId, Uri sourceUrl)
    {
        return
            FromLinkedInGuest(html, jobId, sourceUrl) ??
            FromJsonLd(html, jobId, sourceUrl) ?? 
            FromMarkup(html, jobId, sourceUrl);
    }

    /// <summary>
    /// LinkedIn guest job page. Class names taken from a saved copy of a public job page (October 2026);
    /// LinkedIn can change them at any time, in which case this returns null and the generic fallback runs.
    /// </summary>
    private static JobPosting? FromLinkedInGuest(string html, string jobId, Uri sourceUrl)
    {
        var title = ElementText(html, "h1", "topcard__title");
        if (title is null)
        {
            return null;
        }

        var details = new List<JobDetail>();
        foreach (Match m in CriteriaItems().Matches(html))
        {
            var label = HtmlToText(m.Groups[1].Value);
            var value = HtmlToText(m.Groups[2].Value);
            if (label.Length > 0 && value.Length > 0)
            {
                details.Add(new JobDetail(label, value));
            }
        }

        if (ElementText(html, "span", "num-applicants__caption") is { } applicants)
        {
            details.Add(new JobDetail("Applicants", applicants));
        }

        var description = LinkedInDescription().Match(html);

        return new JobPosting
        {
            JobId = jobId,
            SourceUrl = sourceUrl,
            Title = title,
            Company = ElementText(html, "a", "topcard__org-name-link"),
            Location = LinkedInLocation().Match(html) is { Success: true } loc ? HtmlToText(loc.Groups[1].Value) : null,
            Posted = ElementText(html, "span", "posted-time-ago__text"),
            Details = details,
            Description = description.Success ? HtmlToText(description.Groups[1].Value) : null,
            ParsedFrom = "LinkedIn public job page markup",
        };
    }

    /// <summary>Text of the first &lt;tag&gt; whose class list contains <paramref name="cssClass"/>. Assumes no nested tag of the same name.</summary>
    private static string? ElementText(string html, string tag, string cssClass)
    {
        var m = Regex.Match(
            html,
            $@"<{tag}\b[^>]*\bclass=""[^""]*\b{Regex.Escape(cssClass)}\b[^""]*""[^>]*>(.*?)</{tag}\s*>",
            RegexOptions.IgnoreCase | RegexOptions.Singleline);
        if (!m.Success)
        {
            return null;
        }
        var text = HtmlToText(m.Groups[1].Value);
        return text.Length > 0 ? text : null;
    }

    private static JobPosting? FromJsonLd(string html, string jobId, Uri sourceUrl)
    {
        foreach (Match m in JsonLdBlocks().Matches(html))
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
                if (FindJobPosting(doc.RootElement) is { } job)
                {
                    return new JobPosting
                    {
                        JobId = jobId,
                        SourceUrl = sourceUrl,
                        Title = Str(job, "title"),
                        Company = Str(job, "hiringOrganization", "name") ?? Str(job, "hiringOrganization"),
                        Location = LocationOf(job),
                        Posted = Str(job, "datePosted"),
                        EmploymentType = Str(job, "employmentType"),
                        Description = HtmlToText(Str(job, "description") ?? string.Empty),
                        ParsedFrom = "structured job data (JSON-LD)",
                    };
                }
            }
        }
        return null;
    }

    private static JsonElement? FindJobPosting(JsonElement e)
    {
        switch (e.ValueKind)
        {
            case JsonValueKind.Array:
                foreach (var item in e.EnumerateArray())
                {
                    if (FindJobPosting(item) is { } found)
                    {
                        return found;
                    }
                }
                break;

            case JsonValueKind.Object:
                if (e.TryGetProperty("@type", out var type) && IsJobPosting(type))
                {
                    return e;
                }
                if (e.TryGetProperty("@graph", out var graph))
                {
                    return FindJobPosting(graph);
                }
                break;
        }
        return null;
    }

    private static bool IsJobPosting(JsonElement type) => type.ValueKind switch
    {
        JsonValueKind.String => type.GetString() == "JobPosting",
        JsonValueKind.Array => type.EnumerateArray().Any(t => t.ValueKind == JsonValueKind.String && t.GetString() == "JobPosting"),
        _ => false,
    };

    private static string? LocationOf(JsonElement job)
    {
        if (!job.TryGetProperty("jobLocation", out var loc))
        {
            return Str(job, "jobLocationType");
        }
        if (loc.ValueKind == JsonValueKind.Array)
        {
            loc = loc.EnumerateArray().FirstOrDefault();
        }
        if (loc.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        var parts = new[]
        {
            Str(loc, "address", "addressLocality"),
            Str(loc, "address", "addressRegion"),
            Str(loc, "address", "addressCountry", "name") ?? Str(loc, "address", "addressCountry"),
        };
        var text = string.Join(", ", parts.Where(p => !string.IsNullOrWhiteSpace(p)));
        return text.Length > 0 ? text : Str(loc, "name");
    }

    /// <summary>Reads a string at a property path. Arrays of strings are joined with ", ".</summary>
    private static string? Str(JsonElement e, params string[] path)
    {
        foreach (var name in path)
        {
            if (e.ValueKind != JsonValueKind.Object || !e.TryGetProperty(name, out e))
            {
                return null;
            }
        }

        return e.ValueKind switch
        {
            JsonValueKind.String => e.GetString()?.Trim(),
            JsonValueKind.Number => e.GetRawText(),
            JsonValueKind.Array => string.Join(", ", e.EnumerateArray()
                .Where(x => x.ValueKind == JsonValueKind.String)
                .Select(x => x.GetString())),
            _ => null,
        };
    }

    private static JobPosting FromMarkup(string html, string jobId, Uri sourceUrl)
    {
        var title = Meta(html, "og:title") ?? FirstText(H1(), html) ?? FirstText(TitleTag(), html);
        var description = Meta(html, "og:description") ?? Meta(html, "description");
        var body = HtmlToText(html);

        return new JobPosting
        {
            JobId = jobId,
            SourceUrl = sourceUrl,
            Title = title,
            Company = Meta(html, "og:site_name"),
            Description = body.Length > (description?.Length ?? 0) ? body : description,
            ParsedFrom = "page markup (no structured job data found)",
        };
    }

    private static string? Meta(string html, string name)
    {
        foreach (Match m in MetaTags().Matches(html))
        {
            var tag = m.Value;
            var key = Attr(tag, "property") ?? Attr(tag, "name");
            if (string.Equals(key, name, StringComparison.OrdinalIgnoreCase))
            {
                var content = Attr(tag, "content");
                // Decoded twice: some sites double-escape meta content (&amp;amp;).
                return string.IsNullOrWhiteSpace(content) ? null : WebUtility.HtmlDecode(WebUtility.HtmlDecode(content)).Trim();
            }
        }
        return null;
    }

    private static string? Attr(string tag, string name)
    {
        var m = Regex.Match(tag, $@"\b{name}\s*=\s*(""([^""]*)""|'([^']*)')", RegexOptions.IgnoreCase);
        return m.Success ? (m.Groups[2].Success ? m.Groups[2].Value : m.Groups[3].Value) : null;
    }

    private static string? FirstText(Regex regex, string html)
    {
        var m = regex.Match(html);
        if (!m.Success)
        {
            return null;
        }
        var text = HtmlToText(m.Groups[1].Value);
        return text.Length > 0 ? text : null;
    }

    /// <summary>Turns HTML into readable text: drops scripts and styles, keeps paragraph and list breaks. Cleaning basically</summary>
    public static string HtmlToText(string html)
    {
        var text = NonVisible().Replace(html, " ");
        text = LineBreaks().Replace(text, "\n");
        text = ListItems().Replace(text, "\n- ");
        text = InlineTags().Replace(text, string.Empty);
        text = Tags().Replace(text, " ");
        text = WebUtility.HtmlDecode(text);
        text = Spaces().Replace(text, " ");
        text = Regex.Replace(text, @" *\n *", "\n");
        text = Regex.Replace(text, @"\n{3,}", "\n\n");
        return text.Trim();
    }

    [GeneratedRegex(@"<script[^>]*type\s*=\s*[""']application/ld\+json[""'][^>]*>(.*?)</script>", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex JsonLdBlocks();

    // LinkedIn guest page: the location is the first plain "topcard__flavor--bullet" span (the applicants
    // count shares the bullet class but starts with "num-applicants", so the class must start exactly like this).
    [GeneratedRegex(@"<span class=""topcard__flavor topcard__flavor--bullet""[^>]*>(.*?)</span>", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex LinkedInLocation();

    [GeneratedRegex(@"<div class=""show-more-less-html__markup[^""]*""[^>]*>(.*?)</div>", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex LinkedInDescription();

    [GeneratedRegex(@"<h3 class=""description__job-criteria-subheader""[^>]*>(.*?)</h3>\s*<span class=""description__job-criteria-text[^""]*""[^>]*>(.*?)</span>", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex CriteriaItems();

    [GeneratedRegex(@"<meta\b[^>]*>", RegexOptions.IgnoreCase)]
    private static partial Regex MetaTags();

    [GeneratedRegex(@"<h1\b[^>]*>(.*?)</h1>", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex H1();

    [GeneratedRegex(@"<title\b[^>]*>(.*?)</title>", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex TitleTag();

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
}
