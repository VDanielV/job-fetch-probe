using System.Net;
using JobFetchProbe.Models.Acquisition;

namespace JobFetchProbe.Web.Api;

/// <summary>Typed HTTP client for the API's JobFetcher controller: start and stop (base address and time-out as for the catalog).</summary>
public sealed class JobFetcherClient(HttpClient http, TimeProvider time) : IJobFetcherClient
{
    private const string Route = "api/JobFetcher";

    public async Task<AcquisitionStart> StartAsync(CancellationToken ct = default)
    {
        using var response = await ApiCall.SendAsync(http, () => http.PostAsync(Route, content: null, ct), ct);

        if (response.StatusCode == HttpStatusCode.Conflict)
        {
            var problem = await ApiCall.ReadProblemAsync(response, ct);
            return new AcquisitionStart(
                AcquisitionStartState.AlreadyRunning,
                time.GetLocalNow(),
                problem?.Detail ?? "An acquisition is already running.");
        }

        await ApiCall.EnsureSuccessAsync(response, ct);
        var start = await response.Content.ReadFromJsonAsync<AcquisitionStart>(ApiJson.Options, ct);
        if (start is null)
        {
            throw new InvalidOperationException("The API returned an empty answer.");
        }
        return start;
    }

    public async Task<AcquisitionStop> StopAsync(CancellationToken ct = default)
    {
        using var response = await ApiCall.SendAsync(http, () => http.PostAsync($"{Route}/stop", content: null, ct), ct);

        if (response.StatusCode == HttpStatusCode.Conflict)
        {
            var problem = await ApiCall.ReadProblemAsync(response, ct);
            return new AcquisitionStop(
                AcquisitionStopState.NotRunning,
                time.GetLocalNow(),
                problem?.Detail ?? "No acquisition is running.");
        }

        await ApiCall.EnsureSuccessAsync(response, ct);
        var stop = await response.Content.ReadFromJsonAsync<AcquisitionStop>(ApiJson.Options, ct);
        if (stop is null)
        {
            throw new InvalidOperationException("The API returned an empty answer.");
        }
        return stop;
    }
}
