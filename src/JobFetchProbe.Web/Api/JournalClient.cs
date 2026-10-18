using System.Net;
using JobFetchProbe.Models.Journal;

namespace JobFetchProbe.Web.Api;

/// <summary>Typed HTTP client for the API's Journal controller (base address and time-out as for the catalog).</summary>
public sealed class JournalClient(HttpClient http) : IJournalClient
{
    private const string Route = "api/Journal";

    public async Task<IReadOnlyList<JournalRunSummary>> GetRecentRunsAsync(int take, CancellationToken ct = default)
    {
        using var response = await ApiCall.SendAsync(http, () => http.GetAsync($"{Route}/runs?take={take}", ct), ct);
        if (response.StatusCode == HttpStatusCode.ServiceUnavailable)
        {
            return []; // no database yet, so no journal
        }
        response.EnsureSuccessStatusCode();
        var runs = await response.Content.ReadFromJsonAsync<List<JournalRunSummary>>(ApiJson.Options, ct);
        if (runs is null)
        {
            return [];
        }
        return runs;
    }

    public Task<JobListingJournalRun?> GetJobListingRunAsync(long runId, CancellationToken ct = default)
    {
        return GetRunAsync<JobListingJournalRun>($"{Route}/job-listing/runs/{runId}?after=0", ct);
    }

    public Task<JobDetailsJournalRun?> GetJobDetailsRunAsync(long runId, CancellationToken ct = default)
    {
        return GetRunAsync<JobDetailsJournalRun>($"{Route}/job-details/runs/{runId}?after=0", ct);
    }

    private async Task<T?> GetRunAsync<T>(string path, CancellationToken ct) where T : class
    {
        using var response = await ApiCall.SendAsync(http, () => http.GetAsync(path, ct), ct);
        if (response.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.ServiceUnavailable)
        {
            return null;
        }
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<T>(ApiJson.Options, ct);
    }
}
