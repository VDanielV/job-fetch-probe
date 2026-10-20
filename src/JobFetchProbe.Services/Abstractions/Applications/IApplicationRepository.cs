using JobFetchProbe.Models.Applications;

namespace JobFetchProbe.Services.Abstractions.Applications;

/// <summary>Where applications and their timelines are kept. Implemented by the data layer; never changes Jobs.</summary>
public interface IApplicationRepository
{
    /// <summary>
    /// Records the application at stage Applied with its first timeline line, in one transaction. Nothing is written for
    /// an unknown, archived or already applied job; the outcome says which.
    /// </summary>
    Task<ApplyOutcome> AddAsync(long jobId, DateTimeOffset at, string firstLine, CancellationToken ct = default);

    /// <summary>Every application, open and closed, with its job's title, company and location.</summary>
    Task<IReadOnlyList<ApplicationRecord>> ListAsync(CancellationToken ct = default);

    /// <summary>One application, or null.</summary>
    Task<ApplicationRecord?> GetAsync(long jobId, CancellationToken ct = default);

    /// <summary>The timeline, newest line first.</summary>
    Task<IReadOnlyList<ApplicationNote>> GetNotesAsync(long jobId, CancellationToken ct = default);

    /// <summary>Sets the stage and adds a Stage line, in one transaction. False when there is no such application.</summary>
    Task<bool> ChangeStageAsync(long jobId, ApplicationStage stage, DateTimeOffset at, string line, CancellationToken ct = default);

    /// <summary>Adds an owner's note. False when there is no such application.</summary>
    Task<bool> AddNoteAsync(long jobId, string text, DateTimeOffset at, CancellationToken ct = default);

    /// <summary>Deletes the application; its timeline goes with it (cascade). False when there is no such application.</summary>
    Task<bool> DeleteAsync(long jobId, CancellationToken ct = default);
}
