using System.Text.RegularExpressions;
using JobFetchProbe.Models.Search;
using JobFetchProbe.Services.Parsing;

namespace JobFetchProbe.Services.FetchJobListing;

/// <summary>
/// Reads the job links from a search results page, trying three layouts in order:
/// 1. LinkedIn's public (signed-out) search page: one card per job, marked data-entity-urn="urn:li:jobPosting:&lt;id&gt;";
/// 2. LinkedIn's signed-in search page (/jobs/search-results/): one card per job, marked
///    componentkey="job-card-component-ref-&lt;id&gt;"; its class names are generated, so only that key and the order of
///    the card's paragraphs (title, company, location) are used;
/// 3. generic fallback: every link to a /jobs/view/ page.
/// Duplicates (same job ID) are dropped, keeping page order. Only the jobs present in the page are read;
/// further result pages are not requested.
/// </summary>
public sealed partial class SearchPageParser : ISearchPageParser
{
    /// <summary>Longest stretch read for the last signed-in card, which has no next card to end it.</summary>
    private const int MaxSignedInCardLength = 20_000;

    public ParsedSearch Parse(string html, Uri pageUrl)
    {
        var query = HtmlQuery.ClassText(html, "results-context-header__query-search")
                    ?? UrlHelper.QueryValue(pageUrl, "keywords")
                    ?? HtmlQuery.Title(html);
        var total = ParseCount(HtmlQuery.ClassText(html, "results-context-header__job-count"))
                    ?? ParseCount(HtmlQuery.FirstText(ResultCount(), html));

        var jobs = FromLinkedInCards(html, pageUrl);
        var parsedFrom = "LinkedIn public search page markup";
        if (jobs.Count == 0)
        {
            jobs = FromSignedInCards(html, pageUrl);
            parsedFrom = "LinkedIn signed-in search page markup";
        }
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

            var end = html.Length;
            if (i + 1 < starts.Count)
            {
                end = starts[i + 1].Index;
            }
            var card = html[starts[i].Index..end];

            string? listedAt = null;
            var listDate = ListDate().Match(card);
            if (listDate.Success)
            {
                listedAt = listDate.Groups[1].Value;
            }

            jobs.Add(new JobListing
            {
                SourceJobId = id,
                Url = CardUrl(card, id, pageUrl),
                Title = HtmlQuery.ClassText(card, "base-search-card__title"),
                Company = HtmlQuery.ClassText(card, "base-search-card__subtitle"),
                Location = HtmlQuery.ClassText(card, "job-search-card__location"),
                ListedAt = listedAt,
            });
        }

        return jobs;
    }

    /// <summary>
    /// LinkedIn's signed-in search cards. A card runs from its componentkey to the next card's (the key appears twice per
    /// card, on two nested elements). Its first paragraphs are the title, the company and the location; later ones
    /// ("Visto", salary…) are ignored. The cards link back to the search, so each job's address is built from its ID.
    /// </summary>
    private static List<JobListing> FromSignedInCards(string html, Uri pageUrl)
    {
        var starts = SignedInCardStart().Matches(html);
        var jobs = new List<JobListing>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        for (var i = 0; i < starts.Count; i++)
        {
            var id = starts[i].Groups[1].Value;
            if (!seen.Add(id))
            {
                continue;
            }

            var next = i + 1;
            while (next < starts.Count && starts[next].Groups[1].Value == id)
            {
                next++;
            }
            var end = Math.Min(html.Length, starts[i].Index + MaxSignedInCardLength);
            if (next < starts.Count)
            {
                end = starts[next].Index;
            }
            var card = html[starts[i].Index..end];
            var paragraphs = Paragraph().Matches(card).Select(m => m.Groups[1].Value).ToList();

            jobs.Add(new JobListing
            {
                SourceJobId = id,
                Url = CardUrl(card, id, pageUrl),
                Title = SignedInTitle(paragraphs.ElementAtOrDefault(0)),
                Company = TextAt(paragraphs, 1),
                Location = TextAt(paragraphs, 2),
            });
        }

        return jobs;
    }

    private static string? TextAt(List<string> paragraphs, int index)
    {
        if (index >= paragraphs.Count)
        {
            return null;
        }
        return HtmlQuery.TextOrNull(paragraphs[index]);
    }

    /// <summary>
    /// The title paragraph holds the title twice: first for screen readers, with a label such as "(Empleo verificado)"
    /// added, then the visible title. The visible one is used; without it, the first.
    /// </summary>
    private static string? SignedInTitle(string? paragraph)
    {
        if (paragraph is null)
        {
            return null;
        }

        var firstSpanEnd = paragraph.IndexOf("</span>", StringComparison.OrdinalIgnoreCase);
        if (paragraph.TrimStart().StartsWith("<span", StringComparison.OrdinalIgnoreCase) && firstSpanEnd >= 0)
        {
            var visible = HtmlQuery.TextOrNull(paragraph[(firstSpanEnd + "</span>".Length)..]);
            if (visible is not null)
            {
                return visible;
            }
        }
        return HtmlQuery.TextOrNull(paragraph);
    }

    /// <summary>The card's /jobs/view/ link without query or fragment; if the card has none, the canonical form for the ID.</summary>
    private static Uri CardUrl(string card, string id, Uri pageUrl)
    {
        foreach (Match m in JobViewHref().Matches(card))
        {
            if (UrlHelper.WithoutQuery(m.Groups[1].Value, pageUrl) is { } url && JobIdResolver.FromUrl(url) == id)
            {
                return url;
            }
        }

        var host = "www.linkedin.com";
        if (pageUrl.Host.EndsWith("linkedin.com", StringComparison.OrdinalIgnoreCase))
        {
            host = pageUrl.Host;
        }
        return new Uri($"https://{host}/jobs/view/{id}/");
    }

    /// <summary>Fallback: any link to a /jobs/view/ page that ends in a job ID.</summary>
    private static List<JobListing> FromJobLinks(string html, Uri pageUrl)
    {
        var jobs = new List<JobListing>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (Match m in JobViewAnchor().Matches(html))
        {
            if (UrlHelper.WithoutQuery(m.Groups[1].Value, pageUrl) is not { } url ||
                JobIdResolver.FromUrl(url) is not { } id ||
                !seen.Add(id))
            {
                continue;
            }

            jobs.Add(new JobListing { SourceJobId = id, Url = url, Title = HtmlQuery.TextOrNull(m.Groups[2].Value) });
        }

        return jobs;
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
        var visible = HtmlText.ToText(html);
        return visible.Contains("captcha", StringComparison.OrdinalIgnoreCase) ||
               visible.Contains("security verification", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>The digits of "1,234 results" as a number.</summary>
    private static int? ParseCount(string? text)
    {
        if (text is null)
        {
            return null;
        }
        var digits = new string(text.Where(char.IsDigit).ToArray());
        if (int.TryParse(digits, out var n))
        {
            return n;
        }
        return null;
    }

    // Start of a result card: the opening tag that carries the job URN (a <div> or an <a>, depending on the card).
    [GeneratedRegex(@"<\w+\b[^>]*\bdata-entity-urn=""urn:li:jobPosting:(\d+)""", RegexOptions.IgnoreCase)]
    private static partial Regex CardStart();

    // Start of a signed-in search card.
    [GeneratedRegex(@"\bcomponentkey=""job-card-component-ref-(\d+)""", RegexOptions.IgnoreCase)]
    private static partial Regex SignedInCardStart();

    [GeneratedRegex(@"<p\b[^>]*>(.*?)</p\s*>", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex Paragraph();

    // "3 resultados" / "1,234 results" in its own paragraph (signed-in search page).
    [GeneratedRegex(@"<p\b[^>]*>\s*([\d.,]+\s+(?:resultados?|results?))\s*</p\s*>", RegexOptions.IgnoreCase)]
    private static partial Regex ResultCount();

    [GeneratedRegex(@"href=""([^""]*/jobs/view/[^""]*)""", RegexOptions.IgnoreCase)]
    private static partial Regex JobViewHref();

    [GeneratedRegex(@"<a\b[^>]*\bhref=""([^""]*/jobs/view/[^""]*)""[^>]*>(.*?)</a\s*>", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex JobViewAnchor();

    // Attribute order varies between cards (class before datetime, or the reverse), so the class is checked with a lookahead.
    [GeneratedRegex(@"<time\b(?=[^>]*\bclass=""job-search-card__listdate)[^>]*\bdatetime=""([^""]+)""", RegexOptions.IgnoreCase)]
    private static partial Regex ListDate();

    [GeneratedRegex(@"<meta\b[^>]*http-equiv=""refresh""", RegexOptions.IgnoreCase)]
    private static partial Regex MetaRefresh();
}
