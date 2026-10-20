namespace JobFetchProbe.Services.FetchJobDetails;

/// <summary>
/// What step 2 does with a job right after its description is stored: today, scoring its fit (implemented by the Fit
/// feature, so this step never refers to it). Its time adds to the pause before the next request.
/// Throws <see cref="OperationCanceledException"/> when the run is stopped; any other failure is reported in the result.
/// </summary>
public interface IJobReadFollowUp
{
    Task<JobReadFollowUpResult> AfterReadAsync(long jobId, CancellationToken ct);
}
