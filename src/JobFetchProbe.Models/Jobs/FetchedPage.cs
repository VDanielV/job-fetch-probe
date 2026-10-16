namespace JobFetchProbe.Models.Jobs;

/// <summary>Result of downloading one page.</summary>
/// <param name="Blocked">The site refused the request or redirected to a sign-in page.</param>
/// <param name="Error">Transport failure or unexpected HTTP status; null when the page arrived.</param>
public sealed record FetchedPage(int Status, Uri FinalUrl, string Html, bool Blocked, string? Error);
