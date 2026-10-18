using JobFetchProbe.Models.Archive;

namespace JobFetchProbe.Web.Api;

/// <summary>
/// The call the web app makes to the API's ArchivedJobs controller. Throws <see cref="ApiUnavailableException"/> when the
/// API is not reachable, and <see cref="ApiErrorException"/> with the API's message when it refuses the request.
/// </summary>
public interface IJobArchiveClient
{
    /// <summary>POST api/ArchivedJobs: archives the jobs with one reason and note.</summary>
    Task<ArchiveResult> ArchiveAsync(ArchiveRequest request, CancellationToken ct = default);
}
