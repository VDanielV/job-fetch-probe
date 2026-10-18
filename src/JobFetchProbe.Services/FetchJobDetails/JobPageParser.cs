using System.Text.Json;
using System.Text.RegularExpressions;
using JobFetchProbe.Models.Jobs;
using JobFetchProbe.Services.Parsing;

namespace JobFetchProbe.Services.FetchJobDetails;

/// <summary>
/// Pulls the job out of a page, trying three layouts in order:
/// 1. LinkedIn's public (guest) job page, identified by its top-card and description classes;
/// 2. schema.org JobPosting data embedded for search engines (&lt;script type="application/ld+json"&gt;);
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
    /// LinkedIn can change them at any time, in which case this returns null and the next layout is tried.
    /// </summary>
    private static JobPosting? FromLinkedInGuest(string html, string jobId, Uri sourceUrl)
    {
        var title = HtmlQuery.ClassText(html, "topcard__title", "h1");
        if (title is null)
        {
            return null;
        }

        var details = new List<JobDetail>();
        foreach (Match m in CriteriaItems().Matches(html))
        {
            var label = HtmlText.ToText(m.Groups[1].Value);
            var value = HtmlText.ToText(m.Groups[2].Value);
            if (label.Length > 0 && value.Length > 0)
            {
                details.Add(new JobDetail(label, value));
            }
        }

        if (HtmlQuery.ClassText(html, "num-applicants__caption", "span") is { } applicants)
        {
            details.Add(new JobDetail("Applicants", applicants));
        }

        string? location = null;
        var locationMatch = LinkedInLocation().Match(html);
        if (locationMatch.Success)
        {
            location = HtmlText.ToText(locationMatch.Groups[1].Value);
        }

        string? description = null;
        var descriptionMatch = LinkedInDescription().Match(html);
        if (descriptionMatch.Success)
        {
            description = HtmlText.ToText(descriptionMatch.Groups[1].Value);
        }

        return new JobPosting
        {
            JobId = jobId,
            SourceUrl = sourceUrl,
            Title = title,
            Company = HtmlQuery.ClassText(html, "topcard__org-name-link", "a"),
            Location = location,
            Posted = HtmlQuery.ClassText(html, "posted-time-ago__text", "span"),
            Details = details,
            Description = description,
            ParsedFrom = "LinkedIn public job page markup",
        };
    }

    private static JobPosting? FromJsonLd(string html, string jobId, Uri sourceUrl)
    {
        return JsonLd.ReadFirst(html, "JobPosting", job => new JobPosting
        {
            JobId = jobId,
            SourceUrl = sourceUrl,
            Title = JsonLd.String(job, "title"),
            Company = JsonLd.String(job, "hiringOrganization", "name") ?? JsonLd.String(job, "hiringOrganization"),
            Location = LocationOf(job),
            Posted = JsonLd.String(job, "datePosted"),
            EmploymentType = JsonLd.String(job, "employmentType"),
            Description = HtmlText.ToText(JsonLd.String(job, "description") ?? string.Empty),
            ParsedFrom = "structured job data (JSON-LD)",
        });
    }

    /// <summary>"Locality, Region, Country" from the job's first jobLocation; its name, or the location type (remote), otherwise.</summary>
    private static string? LocationOf(JsonElement job)
    {
        if (!job.TryGetProperty("jobLocation", out var loc))
        {
            return JsonLd.String(job, "jobLocationType");
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
            JsonLd.String(loc, "address", "addressLocality"),
            JsonLd.String(loc, "address", "addressRegion"),
            JsonLd.String(loc, "address", "addressCountry", "name") ?? JsonLd.String(loc, "address", "addressCountry"),
        };
        var text = string.Join(", ", parts.Where(p => !string.IsNullOrWhiteSpace(p)));
        if (text.Length > 0)
        {
            return text;
        }
        return JsonLd.String(loc, "name");
    }

    private static JobPosting FromMarkup(string html, string jobId, Uri sourceUrl)
    {
        var title = HtmlQuery.Meta(html, "og:title") ?? HtmlQuery.FirstHeading(html) ?? HtmlQuery.Title(html);
        var summary = HtmlQuery.Meta(html, "og:description") ?? HtmlQuery.Meta(html, "description");
        var body = HtmlText.ToText(html);

        // The visible text, unless the summary in the meta tags is longer.
        var description = summary;
        if (body.Length > (summary?.Length ?? 0))
        {
            description = body;
        }

        return new JobPosting
        {
            JobId = jobId,
            SourceUrl = sourceUrl,
            Title = title,
            Company = HtmlQuery.Meta(html, "og:site_name"),
            Description = description,
            ParsedFrom = "page markup (no structured job data found)",
        };
    }

    // LinkedIn guest page: the location is the first plain "topcard__flavor--bullet" span (the applicants
    // count shares the bullet class but starts with "num-applicants", so the class must start exactly like this).
    [GeneratedRegex(@"<span class=""topcard__flavor topcard__flavor--bullet""[^>]*>(.*?)</span>", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex LinkedInLocation();

    [GeneratedRegex(@"<div class=""show-more-less-html__markup[^""]*""[^>]*>(.*?)</div>", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex LinkedInDescription();

    [GeneratedRegex(@"<h3 class=""description__job-criteria-subheader""[^>]*>(.*?)</h3>\s*<span class=""description__job-criteria-text[^""]*""[^>]*>(.*?)</span>", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex CriteriaItems();
}
