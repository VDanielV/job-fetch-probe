using JobFetchProbe.Models.Fit;
using JobFetchProbe.Services.Chat;
using JobFetchProbe.Services.FetchJobDetails;

namespace JobFetchProbe.Services.Fit;

/// <summary>
/// Step 2's follow-up: scores the job just read, with the same Check fit as the details pane (the score is stored by
/// <see cref="IJobFitService"/>). A failure is returned, never thrown, so the acquisition goes on; a stop is thrown.
/// </summary>
public sealed class FitAfterRead(IJobFitService fits) : IJobReadFollowUp
{
    public async Task<JobReadFollowUpResult> AfterReadAsync(long jobId, CancellationToken ct)
    {
        try
        {
            var fit = await fits.AssessAsync(new JobFitRequest(jobId), ct);
            if (fit is null)
            {
                return new JobReadFollowUpResult(null, $"job {jobId} is not in the database");
            }
            return new JobReadFollowUpResult(fit, null);
        }
        catch (LocalChatUnavailableException ex)
        {
            return new JobReadFollowUpResult(null, ex.Message);
        }
        catch (JobFitRequestException ex)
        {
            return new JobReadFollowUpResult(null, ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            return new JobReadFollowUpResult(null, ex.Message); // a Fit file is missing or empty
        }
    }
}
