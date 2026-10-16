using JobFetchProbe.Models.Catalog;
using JobFetchProbe.Services.Abstractions;

namespace JobFetchProbe.Services.Catalog;

/// <summary>
/// The rules for reading stored jobs. The data comes from <see cref="IJobCatalog"/>, implemented by the data layer,
/// which also says whether the database exists.
/// </summary>
public sealed class JobRetriever(IJobCatalog catalog) : IJobRetriever
{
    public const int DefaultPageSize = 25;
    public const int MaxPageSize = 100;

    public async Task<JobSummaryPage> GetPageAsync(int page, int pageSize, CancellationToken ct = default)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(page, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(pageSize, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(pageSize, MaxPageSize);
        EnsureAvailable();
        return await catalog.GetPageAsync(page, pageSize, ct);
    }

    public async Task<StoredJob?> GetJobAsync(string jobId, CancellationToken ct = default)
    {
        EnsureAvailable();
        var id = jobId?.Trim();
        return string.IsNullOrEmpty(id) ? null : await catalog.GetAsync(id, ct);
    }

    private void EnsureAvailable()
    {
        if (!catalog.IsAvailable)
        {
            throw new JobStoreUnavailableException();
        }
    }
}
