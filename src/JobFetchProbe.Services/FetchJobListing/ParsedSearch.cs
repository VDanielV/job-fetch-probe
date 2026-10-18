using JobFetchProbe.Models.Search;


namespace JobFetchProbe.Services.FetchJobListing;

/// <summary>What the search page parser found, before the search is named and stored.</summary>
/// <param name="LooksLikeChallenge">No results, and the page is a bot check (meta refresh, challenge cookie script, captcha text).</param>
public sealed record ParsedSearch(
    IReadOnlyList<JobListing> Jobs,
    string? Query,
    int? TotalResults,
    string ParsedFrom,
    bool LooksLikeChallenge);
