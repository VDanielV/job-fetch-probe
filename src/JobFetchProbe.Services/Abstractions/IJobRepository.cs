using JobFetchProbe.Models.Jobs;
using JobFetchProbe.Models.Search;

namespace JobFetchProbe.Services.Abstractions;

/// <summary>The jobs table: the work list of step 2 and where each result is stored. Implemented with the database.</summary>
public interface IJobRepository
{
    /// <summary>
    /// Every job without a description that is worth trying: not read yet, or refused / failed before.
    /// Jobs the site reported as gone, archived jobs and jobs applied to are left out. Never-tried jobs come first, oldest first.
    /// </summary>
    Task<IReadOnlyList<JobListing>> GetUnreadAsync(CancellationToken ct = default);

    /// <summary>Stores what the job page showed and marks the job as read. Returns the job's app ID.</summary>
    Task<long> SaveDescriptionAsync(JobPosting job, CancellationToken ct = default);

    /// <summary>Records a failed attempt. <paramref name="gone"/>: the page no longer exists (HTTP 404/410), so it is not tried again.</summary>
    Task SaveFailureAsync(string source, string sourceJobId, JobResultStatus status, bool gone, string message, CancellationToken ct = default);

    /// <summary>Number of jobs with a description.</summary>
    Task<int> CountReadAsync(CancellationToken ct = default);
}
