using JobFetchProbe.Models.Fit;

namespace JobFetchProbe.Services.Abstractions.Fit;

/// <summary>Where the fit assessments are kept, every one of them. Implemented by the data layer.</summary>
public interface IJobFitAssessmentStore
{
    /// <summary>Stores one assessment of the job (its Id is given by the store).</summary>
    Task AddAsync(string jobId, JobFitAssessment assessment, CancellationToken ct = default);

    /// <summary>Every assessment of the job, newest first; empty when it was never assessed.</summary>
    Task<IReadOnlyList<JobFitAssessment>> GetAsync(string jobId, CancellationToken ct = default);
}
