using System.Net;
using JobFetchProbe.Models.Catalog;
using JobFetchProbe.Models.Filter;
using Microsoft.AspNetCore.Mvc;

namespace JobFetchProbe.Web.Api;

/// <summary>
/// Typed HTTP client for the API's JobCatalog controller. Receives its HttpClient (base address and timeout from
/// <see cref="JobCatalogApiSettings"/>) from IHttpClientFactory and never creates or disposes one.
/// </summary>
public sealed class JobCatalogClient(HttpClient http) : IJobCatalogClient
{
    private const string Route = "api/JobCatalog";

    public async Task<JobSummaryPage> GetPageAsync(
        int page, int pageSize, bool includeArchived = false, string? query = null, FilterScope scope = FilterScope.All,
        IReadOnlyList<string>? places = null, IReadOnlyList<string>? modes = null, JobSort sort = JobSort.Newest,
        CancellationToken ct = default)
    {
        var path = $"{Route}?page={page}&pageSize={pageSize}";
        if (includeArchived)
        {
            path += "&includeArchived=true";
        }
        if (!string.IsNullOrWhiteSpace(query))
        {
            path += "&q=" + Uri.EscapeDataString(query.Trim());
            if (scope == FilterScope.Title)
            {
                path += "&in=title";
            }
        }
        foreach (var place in places ?? [])
        {
            path += "&place=" + Uri.EscapeDataString(place);
        }
        foreach (var mode in modes ?? [])
        {
            path += "&mode=" + Uri.EscapeDataString(mode);
        }
        if (sort == JobSort.Fit)
        {
            path += "&sort=Fit";
        }
        using var response = await SendAsync(path, ct);
        await ThrowIfUnavailableAsync(response, ct);
        await ApiCall.EnsureSuccessAsync(response, ct);
        return await response.Content.ReadFromJsonAsync<JobSummaryPage>(ApiJson.Options, ct)
            ?? throw new InvalidOperationException("The API returned an empty page.");
    }

    public async Task<StoredJob?> GetJobAsync(long jobId, CancellationToken ct = default)
    {
        using var response = await SendAsync($"{Route}/{jobId}", ct);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }
        await ThrowIfUnavailableAsync(response, ct);
        await ApiCall.EnsureSuccessAsync(response, ct);
        return await response.Content.ReadFromJsonAsync<StoredJob>(ApiJson.Options, ct);
    }

    /// <summary>Sends the GET; a connection failure or a timeout means the API is not reachable.</summary>
    private Task<HttpResponseMessage> SendAsync(string path, CancellationToken ct)
    {
        return ApiCall.SendAsync(http, () => http.GetAsync(path, ct), ct);
    }

    /// <summary>503 from the API: no database yet. Its problem details say why; that text is shown as is.</summary>
    private static async Task ThrowIfUnavailableAsync(HttpResponseMessage response, CancellationToken ct)
    {
        if (response.StatusCode != HttpStatusCode.ServiceUnavailable)
        {
            return;
        }
        ProblemDetails? problem = null;
        try
        {
            problem = await response.Content.ReadFromJsonAsync<ProblemDetails>(ct);
        }
        catch (System.Text.Json.JsonException)
        {
            // Not problem details: fall back to the generic text below.
        }
        throw new ApiUnavailableException(problem?.Detail ?? "The API cannot read the jobs database right now.");
    }
}
