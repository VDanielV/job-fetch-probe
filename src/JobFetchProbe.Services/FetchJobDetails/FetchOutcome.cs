using JobFetchProbe.Models.Jobs;
using JobFetchProbe.Models.Search;

namespace JobFetchProbe.Services.FetchJobDetails;

/// <summary>What one job page's result means for step 2: read, gone, and what the circuit breaker is told.</summary>
internal static class FetchOutcome
{
    /// <summary>The page was read and holds a job.</summary>
    public static bool IsRead(JobResult result)
    {
        return result.Status == JobResultStatus.Read && result.Job is not null;
    }

    /// <summary>The job page no longer exists (HTTP 404 or 410): the job is not tried again.</summary>
    public static bool IsGone(JobResult result)
    {
        return result.HttpStatus is 404 or 410;
    }

    /// <summary>
    /// Tells the circuit breaker about the result: a read page is a success, a refusal opens the circuit at once, a
    /// network failure or server error counts towards opening it, and anything else (a 404, say) says nothing about
    /// the site's health.
    /// </summary>
    public static void TellBreaker(CircuitBreaker breaker, JobListing job, JobResult result)
    {
        if (IsRead(result))
        {
            breaker.RecordSuccess();
        }
        else if (result.Status == JobResultStatus.Blocked)
        {
            breaker.RecordRefusal($"{job.Url.Host} refused the request for job {job.SourceJobId} (status {result.HttpStatus}: refusal or sign-in page)");
        }
        else if (result.HttpStatus is 0 or >= 500)
        {
            breaker.RecordFailure($"{job.Url.Host} is failing: {result.Message}");
        }
        else
        {
            breaker.RecordNeutral(); // e.g. 404: this job is gone, the site itself is fine
        }
    }
}
