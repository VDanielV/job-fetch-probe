using System.Net;
using System.Text.RegularExpressions;
using JobFetchProbe.Models;
using JobFetchProbe.Services.Parsing;

namespace JobFetchProbe.Services.Search;

/// <summary>
/// Reads the job links from a search results page, trying two layouts in order:
/// 1. LinkedIn's public search page: one card per job, marked data-entity-urn="urn:li:jobPosting:&lt;id&gt;";
/// 2. generic fallback: every link to a /jobs/view/ page.
/// Duplicates (same job ID) are dropped, keeping page order. Only the jobs present in the page are read;
/// further result pages are not requested.
/// </summary>
public sealed partial class SearchPageParser : ISearchPageParser
{
    public ParsedSearch Parse(string html, Uri pageUrl)
    {
        var query = ClassText(html, "results-context-header__query-search")
                    ?? FirstText(TitleTag(), html);
        var total = ParseCount(ClassText(html, "results-context-header__job-count"));

        var jobs = FromLinkedInCards(html, pageUrl);
        var parsedFrom = "LinkedIn public search page markup";
        if (jobs.Count == 0)
        {
            jobs = FromJobLinks(html, pageUrl);
            parsedFrom = "job links in page (no LinkedIn result cards found)";
        }

        return new ParsedSearch(jobs, query, total, parsedFrom, jobs.Count == 0 && IsChallenge(html));
    }

    /// <summary>LinkedIn result cards. Each card starts at the element carrying data-entity-urn and ends where the next one starts.</summary>
    private static List<JobListing> FromLinkedInCards(string html, Uri pageUrl)
    {
        var starts = CardStart().Matches(html);
        var jobs = new List<JobListing>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        for (var i = 0; i < starts.Count; i++)
        {
            var id = starts[i].Groups[1].Value;
            if (!seen.Add(id))
            {
                continue;
            }

            var end = i + 1 < starts.Count ? starts[i + 1].Index : html.Length;
            var card = html[starts[i].Index..end];

            jobs.Add(new JobListing
            {
                JobId = id,
                Url = CardUrl(card, id, pageUrl),
                Title = ClassText(card, "base-search-card__title"),
                Company = ClassText(card, "base-search-card__subtitle"),
                Location = ClassText(card, "job-search-card__location"),
                ListedAt = ListDate().Match(card) is { Success: true } d ? d.Groups[1].Value : null,
            });
        }

        return jobs;
    }

    /// <summary>The card's /jobs/view/ link without query or fragment; if the card has none, the canonical form for the ID.</summary>
    private static Uri CardUrl(string card, string id, Uri pageUrl)
    {
        foreach (Match m in JobViewHref().Matches(card))
        {
            if (Clean(m.Groups[1].Value, pageUrl) is { } url && JobIdResolver.FromUrl(url) == id)
            {
                return url;
            }
        }

        var host = pageUrl.Host.EndsWith("linkedin.com", StringComparison.OrdinalIgnoreCase) ? pageUrl.Host : "www.linkedin.com";
        return new Uri($"https://{host}/jobs/view/{id}/");
    }

    /// <summary>Fallback: any link to a /jobs/view/ page that ends in a job ID.</summary>
    private static List<JobListing> FromJobLinks(string html, Uri pageUrl)
    {
        var jobs = new List<JobListing>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (Match m in JobViewAnchor().Matches(html))
        {
            if (Clean(m.Groups[1].Value, pageUrl) is not { } url || JobIdResolver.FromUrl(url) is not { } id || !seen.Add(id))
            {
                continue;
            }

            var text = JobPageParser.HtmlToText(m.Groups[2].Value);
            jobs.Add(new JobListing { JobId = id, Url = url, Title = text.Length > 0 ? text : null });
        }

        return jobs;
    }

    /// <summary>Decodes, resolves against the page and drops query and fragment (tracking parameters).</summary>
    private static Uri? Clean(string href, Uri pageUrl)
    {
        if (!Uri.TryCreate(pageUrl, WebUtility.HtmlDecode(href), out var url) ||
            (url.Scheme != Uri.UriSchemeHttps && url.Scheme != Uri.UriSchemeHttp))
        {
            return null;
        }
        return new UriBuilder(url) { Query = string.Empty, Fragment = string.Empty }.Uri;
    }

    /// <summary>
    /// A bot-check page: meta refresh, LinkedIn's challenge-cookie script, or captcha/verification wording in the
    /// visible text. The wording is checked in visible text only, because ordinary pages mention "captcha" in scripts.
    /// </summary>
    private static bool IsChallenge(string html)
    {
        if (MetaRefresh().IsMatch(html) || html.Contains("ccookie", StringComparison.Ordinal))
        {
            return true;
        }
        var visible = JobPageParser.HtmlToText(html);
        return visible.Contains("captcha", StringComparison.OrdinalIgnoreCase) ||
               visible.Contains("security verification", StringComparison.OrdinalIgnoreCase);
    }

    private static int? ParseCount(string? text)
    {
        if (text is null)
        {
            return null;
        }
        var digits = new string(text.Where(char.IsDigit).ToArray());
        return int.TryParse(digits, out var n) ? n : null;
    }

    /// <summary>Text of the first element whose class list contains <paramref name="cssClass"/> (inner tags allowed, same tag not nested).</summary>
    private static string? ClassText(string html, string cssClass)
    {
        var m = Regex.Match(
            html,
            $@"<(\w+)\b[^>]*\bclass=""[^""]*\b{Regex.Escape(cssClass)}\b[^""]*""[^>]*>(.*?)</\1\s*>",
            RegexOptions.IgnoreCase | RegexOptions.Singleline);
        if (!m.Success)
        {
            return null;
        }
        var text = JobPageParser.HtmlToText(m.Groups[2].Value);
        return text.Length > 0 ? text : null;
    }

    private static string? FirstText(Regex regex, string html)
    {
        var m = regex.Match(html);
        if (!m.Success)
        {
            return null;
        }
        var text = JobPageParser.HtmlToText(m.Groups[1].Value);
        return text.Length > 0 ? text : null;
    }

    // Start of a result card: the opening tag that carries the job URN (a <div> or an <a>, depending on the card).
    [GeneratedRegex(@"<\w+\b[^>]*\bdata-entity-urn=""urn:li:jobPosting:(\d+)""", RegexOptions.IgnoreCase)]
    private static partial Regex CardStart();

    [GeneratedRegex(@"href=""([^""]*/jobs/view/[^""]*)""", RegexOptions.IgnoreCase)]
    private static partial Regex JobViewHref();

    [GeneratedRegex(@"<a\b[^>]*\bhref=""([^""]*/jobs/view/[^""]*)""[^>]*>(.*?)</a\s*>", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex JobViewAnchor();

    // Attribute order varies between cards (class before datetime, or the reverse), so the class is checked with a lookahead.
    [GeneratedRegex(@"<time\b(?=[^>]*\bclass=""job-search-card__listdate)[^>]*\bdatetime=""([^""]+)""", RegexOptions.IgnoreCase)]
    private static partial Regex ListDate();

    [GeneratedRegex(@"<meta\b[^>]*http-equiv=""refresh""", RegexOptions.IgnoreCase)]
    private static partial Regex MetaRefresh();

    [GeneratedRegex(@"<title\b[^>]*>(.*?)</title>", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex TitleTag();
}
