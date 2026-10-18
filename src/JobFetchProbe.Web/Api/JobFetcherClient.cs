using System.Net;
using System.Text.Json;
using JobFetchProbe.Models.Acquisition;
using Microsoft.AspNetCore.Mvc;

namespace JobFetchProbe.Web.Api;

/// <summary>Typed HTTP client for the API's JobFetcher controller (base address and time-out as for the catalog).</summary>
public sealed class JobFetcherClient(HttpClient http, TimeProvider time) : IJobFetcherClient
{
    private const string Route = "api/JobFetcher";

    public async Task<AcquisitionStart> StartAsync(CancellationToken ct = default)
    {
        using var response = await ApiCall.SendAsync(http, () => http.PostAsync(Route, content: null, ct), ct);

        if (response.StatusCode == HttpStatusCode.Conflict)
        {
            var problem = await ReadProblemAsync(response, ct);
            return new AcquisitionStart(
                AcquisitionStartState.AlreadyRunning,
                time.GetLocalNow(),
                problem?.Detail ?? "An acquisition is already running.");
        }

        response.EnsureSuccessStatusCode();
        var start = await response.Content.ReadFromJsonAsync<AcquisitionStart>(ApiJson.Options, ct);
        if (start is null)
        {
            throw new InvalidOperationException("The API returned an empty answer.");
        }
        return start;
    }

    private static async Task<ProblemDetails?> ReadProblemAsync(HttpResponseMessage response, CancellationToken ct)
    {
        try
        {
            return await response.Content.ReadFromJsonAsync<ProblemDetails>(ct);
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
