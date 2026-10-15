using JobFetchProbe.Models;
using JobFetchProbe.Services.Abstractions;
using JobFetchProbe.Services.Parsing;

namespace JobFetchProbe.Services.Search;

/// <summary>
/// Reads one search results page and stores its job links. One request per link, no pagination:
/// only the jobs already in the page are read.
/// </summary>
public sealed partial class SearchService(
    IPageFetcher fetcher,
    ISavedPageReader savedPages,
    ISearchPageParser parser,
    ISearchRepository repository,
    TimeProvider time) : ISearchService
{
    private const string ChallengeMessage =
        "the page is a bot check, not the search results (LinkedIn sends this to clients it does not trust). " +
        "Open the search in your browser, scroll to load the results you want, save the page, and give the file path instead.";

    public async Task<SearchResult> FromUrlAsync(Uri url, CancellationToken ct = default)
    {
        if (url.Scheme != Uri.UriSchemeHttps && url.Scheme != Uri.UriSchemeHttp)
        {
            return SearchResult.Failed("the link must start with https://");
        }

        var page = await fetcher.GetAsync(url, ct);
        if (page.Blocked)
        {
            return SearchResult.Blocked(
                $"{page.FinalUrl.Host} did not return the search page (status {page.Status}: sign-in or refusal). " +
                "Save the page from your browser and give the file path instead.");
        }
        if (page.Error is not null)
        {
            return SearchResult.Failed($"could not fetch the page: {page.Error}");
        }

        return await SaveIfReadAsync(Build(page.Html, url), ct);
    }

    public async Task<SearchResult> FromFileAsync(string path, CancellationToken ct = default) =>
        await SaveIfReadAsync(await ReadFileAsync(path, ct), ct);

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
            return probe.LooksLikeChallenge
                ? SearchResult.Blocked(ChallengeMessage)
                : SearchResult.Failed(
                    "the saved page does not contain its own address (no canonical link, og:url or \"saved from\" note), so the search's URL is unknown. " +
                    "Open the search in the browser and save it again, or paste the link instead.");
        }

        return Build(html, sourceUrl);
    }

    private async Task<SearchResult> SaveIfReadAsync(SearchResult read, CancellationToken ct)
    {
        if (read.Status != JobResultStatus.Read || read.Search is null)
        {
            return read;
        }
        var path = await repository.SaveAsync(read.Search, ct);
        return SearchResult.Saved(read.Search, path);
    }

    /// <summary>
    /// Browsers note the address of a saved page in a comment: &lt;!-- saved from url=(0114)https://… --&gt;.
    /// Returns it when it is an absolute http(s) address (not "about:internet").
    /// </summary>
    private static Uri? SavedFromUri(string html)
    {
        var m = SavedFrom().Match(html);
        return m.Success &&
               Uri.TryCreate(m.Groups[1].Value, UriKind.Absolute, out var uri) &&
               (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp)
            ? uri
            : null;
    }

    /// <summary>The currentJobId query value of a LinkedIn search address, if any.</summary>
    private static string? CurrentJobId(Uri url)
    {
        foreach (var pair in url.Query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var parts = pair.Split('=', 2);
            if (parts.Length == 2 && parts[0].Equals("currentJobId", StringComparison.OrdinalIgnoreCase) && parts[1].All(char.IsDigit))
            {
                return parts[1];
            }
        }
        return null;
    }

    private SearchResult Build(string html, Uri sourceUrl)
    {
        var parsed = parser.Parse(html, sourceUrl);

        // A view-source save of LinkedIn's signed-in search page holds only the selected job (currentJobId in its
        // address), not the list. Reading that one job as "the results" would be misleading, so it is refused.
        if (parsed.Jobs.Count <= 1 && CurrentJobId(sourceUrl) is { } selected &&
            parsed.Jobs.All(j => j.JobId == selected) && !parsed.ParsedFrom.StartsWith("LinkedIn", StringComparison.Ordinal))
        {
            return SearchResult.Failed(
                "the page only contains the selected job (" + selected + "), not the result list. This is what a view-source " +
                "save of a signed-in LinkedIn search looks like: the list is loaded later by the page's scripts. Save the " +
                "signed-out search page instead, or use \"Save page as → Webpage, Complete\" after the list has loaded.");
        }

        if (parsed.Jobs.Count == 0)
        {
            return parsed.LooksLikeChallenge
                ? SearchResult.Blocked(ChallengeMessage)
                : SearchResult.Failed(
                    "no job links found in the page. If it is a view-source save of a signed-in LinkedIn search, it only contains the " +
                    "selected job: save the signed-out search page instead, or use \"Save page as → Webpage, Complete\" after the list has loaded.");
        }

        // Name the search after the page's canonical address when it has one, so a link and a saved
        // copy of the same search write to the same file.
        var nameSource = JobIdResolver.CanonicalUri(html) ?? sourceUrl;
        var searchId = SearchIdResolver.FromUrl(nameSource)
                       ?? SearchIdResolver.FromUrl(sourceUrl)
                       ?? $"search-{time.GetUtcNow():yyyyMMdd-HHmmss}";

        var search = new JobSearch
        {
            SearchId = searchId,
            SourceUrl = sourceUrl,
            Query = parsed.Query,
            TotalResults = parsed.TotalResults,
            Jobs = parsed.Jobs,
            ParsedFrom = parsed.ParsedFrom,
            RetrievedAt = time.GetUtcNow(),
        };

        return SearchResult.Read(search);
    }

    [System.Text.RegularExpressions.GeneratedRegex(@"<!--\s*saved from url=\(\d+\)(\S+?)\s*-->", System.Text.RegularExpressions.RegexOptions.IgnoreCase)]
    private static partial System.Text.RegularExpressions.Regex SavedFrom();
}
