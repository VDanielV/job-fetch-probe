using JobFetchProbe.Models.Fit;

namespace JobFetchProbe.Web.Api;

/// <summary>
/// The calls the web app makes to the API's JobFit controller: score one stored job with the local model (the API keeps
/// the score), and read the stored scores. Throws
/// <see cref="ApiUnavailableException"/> when the API or Ollama cannot answer, and <see cref="ApiErrorException"/> with the
/// API's message when it refuses the job (no description yet, unknown job).
/// </summary>
public interface IJobFitClient
{
    /// <summary>POST api/JobFit { jobId }. Takes as long as the local model needs (up to JobCatalogApi:LocalModelTimeoutSeconds).</summary>
    Task<JobFitResult> AssessAsync(string jobId, CancellationToken ct = default);

    /// <summary>GET api/JobFit/{jobId}: the stored scores, newest first; null when the job is not in the database.</summary>
    Task<JobFitHistory?> GetHistoryAsync(string jobId, CancellationToken ct = default);
}
