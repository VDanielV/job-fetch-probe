using JobFetchProbe.Models.Fit;

namespace JobFetchProbe.Services.Fit;

/// <summary>
/// Scores one stored job, and keeps every score. Returns null when the id is not in the database.
/// Throws <see cref="JobFitRequestException"/> when the job cannot be scored.
/// Throws <c>LocalChatUnavailableException</c> when Ollama cannot answer.
/// Throws <c>JobStoreUnavailableException</c> when the database does not exist yet.
/// </summary>
public interface IJobFitService
{
    Task<JobFitResult?> AssessAsync(JobFitRequest request, CancellationToken ct = default);

    /// <summary>Every stored score of the job, newest first; null when the id is not in the database.</summary>
    Task<JobFitHistory?> GetHistoryAsync(string jobId, CancellationToken ct = default);
}
