using JobFetchProbe.Models.Fit;

namespace JobFetchProbe.Web.Api;

/// <summary>
/// The call the web app makes to the API's JobFit controller: score one stored job with the local model. Throws
/// <see cref="ApiUnavailableException"/> when the API or Ollama cannot answer, and <see cref="ApiErrorException"/> with the
/// API's message when it refuses the job (no description yet, unknown job).
/// </summary>
public interface IJobFitClient
{
    /// <summary>POST api/JobFit { jobId }. Takes as long as the local model needs (up to about two minutes).</summary>
    Task<JobFitResult> AssessAsync(string jobId, CancellationToken ct = default);
}
