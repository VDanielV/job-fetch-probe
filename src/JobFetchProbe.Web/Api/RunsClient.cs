using System.Net;
using JobFetchProbe.Models.RunHistory;

namespace JobFetchProbe.Web.Api;

/// <summary>Typed HTTP client for the API's Runs controller (base address and time-out as for the catalog).</summary>
public sealed class RunsClient(HttpClient http) : IRunsClient
{
    private const string Route = "api/Runs";

    public Task<RunHistoryPage?> GetPageAsync(int page, int pageSize, bool includeEmpty, CancellationToken ct = default)
    {
        var empty = "false";
        if (includeEmpty)
        {
            empty = "true";
        }
        return GetAsync<RunHistoryPage>($"{Route}?page={page}&pageSize={pageSize}&includeEmpty={empty}", ct);
    }

    public Task<RunHistoryLine?> GetRunAsync(long runId, CancellationToken ct = default)
    {
        return GetAsync<RunHistoryLine>($"{Route}/{runId}", ct);
    }

    public Task<RunJobLinks?> GetJobLinksAsync(long runId, CancellationToken ct = default)
    {
        return GetAsync<RunJobLinks>($"{Route}/{runId}/job-links", ct);
    }

    public Task<RunJobLinks?> GetListingJobLinksAsync(string listingId, CancellationToken ct = default)
    {
        return GetAsync<RunJobLinks>($"{Route}/listings/{Uri.EscapeDataString(listingId)}/job-links", ct);
    }

    /// <summary>Null for 404 (unknown) and 503 (no database yet); other errors throw.</summary>
    private async Task<T?> GetAsync<T>(string path, CancellationToken ct) where T : class
    {
        using var response = await ApiCall.SendAsync(http, () => http.GetAsync(path, ct), ct);
        if (response.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.ServiceUnavailable)
        {
            return null;
        }
        await ApiCall.EnsureSuccessAsync(response, ct);
        return await response.Content.ReadFromJsonAsync<T>(ApiJson.Options, ct);
    }
}
