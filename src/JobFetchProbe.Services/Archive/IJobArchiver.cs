using JobFetchProbe.Models.Archive;

namespace JobFetchProbe.Services.Archive;

/// <summary>
/// Archives jobs the owner will not apply to: they leave the Overview, and step 2 never requests them again. Nothing is
/// deleted. Throws <see cref="ArchiveRequestException"/> for a request that breaks the rules, and
/// <c>JobStoreUnavailableException</c> while the database does not exist yet.
/// </summary>
public interface IJobArchiver
{
    Task<ArchiveResult> ArchiveAsync(ArchiveRequest request, CancellationToken ct = default);
}
