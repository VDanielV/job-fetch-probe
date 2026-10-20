using System.Text.RegularExpressions;
using JobFetchProbe.Models.Search;
using JobFetchProbe.Services.Abstractions;
using JobFetchProbe.Services.Parsing;

namespace JobFetchProbe.Services.FetchJobListing;

/// <summary>
/// Reads the job links in a search results page saved from the browser. No network: only the jobs already in the
/// page are read.
/// </summary>
public sealed partial class SearchService(
    ISavedPageReader savedPages,
    ISearchPageParser parser,
    TimeProvider time) : ISearchService
{
    private const string ChallengeMessage =
        "the page is a bot check, not the search results (LinkedIn sends this to clients it does not trust). " +
        "Open the search in your browser, scroll to load the results you want, and save the page into the input folder.";

    public async Task<SearchResult> ReadFileAsync(string path, CancellationToken ct = default)
    {
        string html;
        try
        {
            html = await savedPages.ReadAsync(path, ct);
        }
        catch (Exception ex) when (ex is FileNotFoundException or DirectoryNotFoundException or UnauthorizedAccessException)
        {
            return SearchResult.Failed($"cannot read file: {ex.Message}");
        }

        // The page's own address: canonical link or og:url, else the "saved from" note browsers add when saving.
        var sourceUrl = JobIdResolver.CanonicalUri(html) ?? SavedFromUri(html);
        if (sourceUrl is null)
        {
            // Check for a bot-check page first: it has no canonical link either, and the cause is different.
            var probe = parser.Parse(html, new Uri("https://www.linkedin.com/"));
            if (probe.LooksLikeChallenge)
            {
                return SearchResult.Blocked(ChallengeMessage);
            }
            return SearchResult.Failed(
                "the saved page does not contain its own address (no canonical link, og:url or \"saved from\" note), so the search's URL is unknown. " +
                "Open the search in the browser and save it again.");
        }

        return Build(html, sourceUrl);
    }

    /// <summary>
    /// Browsers note the address of a saved page in a comment: &lt;!-- saved from url=(0114)https://… --&gt;.
    /// Returns it when it is an absolute http(s) address (not "about:internet").
    /// </summary>
    private static Uri? SavedFromUri(string html)
    {
        var m = SavedFrom().Match(html);
        if (!m.Success)
        {
            return null;
        }
        return UrlHelper.AbsoluteWebUri(m.Groups[1].Value);
    }

    /// <summary>The currentJobId query value of a LinkedIn search address, if it is a number.</summary>
    private static string? CurrentJobId(Uri url)
    {
        var value = UrlHelper.QueryValue(url, "currentJobId");
        if (value is null || !value.All(char.IsDigit))
        {
            return null;
        }
        return value;
    }

    private SearchResult Build(string html, Uri sourceUrl)
    {
        var parsed = parser.Parse(html, sourceUrl);

        // No result cards of a known layout, and the only job found is the one selected in the address (currentJobId):
        // the page holds no list. Reading that one job as "the results" would be misleading, so it is refused. This is
        // what a view-source save of LinkedIn's signed-in pages looks like; a complete save in a new, unknown layout too.
        if (parsed.Jobs.Count <= 1 && CurrentJobId(sourceUrl) is { } selected &&
            parsed.Jobs.All(j => j.SourceJobId == selected) && !parsed.ParsedFrom.StartsWith("LinkedIn", StringComparison.Ordinal))
        {
            return SearchResult.Failed(
                "no result list found, only the selected job (" + selected + "). If this is a view-source save (Ctrl+U), it " +
                "cannot contain the list: LinkedIn loads it later with scripts. Save the page with \"Save page as → Webpage, " +
                "Complete\" after scrolling through the list. If it already is a complete save, LinkedIn's layout may have changed.");
        }

        if (parsed.Jobs.Count == 0)
        {
            if (parsed.LooksLikeChallenge)
            {
                return SearchResult.Blocked(ChallengeMessage);
            }
            return SearchResult.Failed(
                "no job links found in the page. If it is a view-source save of a signed-in LinkedIn search, it only contains the " +
                "selected job: save the signed-out search page instead, or use \"Save page as → Webpage, Complete\" after the list has loaded.");
        }

        var search = new JobSearch
        {
            SourceUrl = sourceUrl,
            Query = parsed.Query,
            TotalResults = parsed.TotalResults,
            Jobs = parsed.Jobs,
            ParsedFrom = parsed.ParsedFrom,
            RetrievedAt = time.GetUtcNow(),
        };

        return SearchResult.Read(search);
    }

    [GeneratedRegex(@"<!--\s*saved from url=\(\d+\)(\S+?)\s*-->", RegexOptions.IgnoreCase)]
    private static partial Regex SavedFrom();
}
