using JobFetchProbe.Models.Acquisition;

namespace JobFetchProbe.Web.Api;

/// <summary>The calls the web app makes to the API's JobFetcher controller: starting and stopping an acquisition.</summary>
public interface IJobFetcherClient
{
    /// <summary>
    /// POST api/JobFetcher. Started (202), or AlreadyRunning (409) with the API's own explanation.
    /// Throws <see cref="ApiUnavailableException"/> when the API is not reachable.
    /// </summary>
    Task<AcquisitionStart> StartAsync(CancellationToken ct = default);

    /// <summary>
    /// POST api/JobFetcher/stop. Stopping (202), or NotRunning (409) with the API's own explanation.
    /// Throws <see cref="ApiUnavailableException"/> when the API is not reachable.
    /// </summary>
    Task<AcquisitionStop> StopAsync(CancellationToken ct = default);
}
