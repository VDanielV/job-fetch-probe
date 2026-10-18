namespace JobFetchProbe.Services.FetchJobListing;

public interface ISearchPageParser
{
    /// <param name="pageUrl">The page's own address, used to resolve relative links.</param>
    ParsedSearch Parse(string html, Uri pageUrl);
}
