using JobFetchProbe.Models.Applications;

namespace JobFetchProbe.Services.Applications;

/// <summary>
/// The jobs the owner applied to: their stage, follow-up flags and notes. Throws <see cref="ApplicationRequestException"/>
/// for a request that breaks the rules, <see cref="ApplicationConflictException"/> when the state does not allow it, and
/// <c>JobStoreUnavailableException</c> while the database does not exist yet. Methods about one application return
/// null when it does not exist.
/// </summary>
public interface IApplicationTracker
{
    /// <summary>"I applied": stage Applied, dated now, with an Applied line. Null for an unknown job.</summary>
    Task<Application?> ApplyAsync(ApplyRequest request, CancellationToken ct = default);

    /// <summary>
    /// Undo of a mistaken "I applied": deletes the application and its timeline. When the owner wrote notes, only with
    /// <paramref name="withNotes"/>; otherwise <see cref="ApplicationConflictException"/> with the number of notes.
    /// False when there is no such application.
    /// </summary>
    Task<bool> UndoAsync(long jobId, bool withNotes, CancellationToken ct = default);

    /// <summary>
    /// One page of the open applications, or the closed ones; optionally only one stage, or only the flagged ones; in the
    /// usual order, or the best latest fit first (never assessed last).
    /// </summary>
    Task<ApplicationPage> GetPageAsync(
        bool closed, ApplicationStage? stage, bool flagged, int page, int pageSize, ApplicationSort sort = ApplicationSort.FollowUp,
        CancellationToken ct = default);

    Task<Application?> GetAsync(long jobId, CancellationToken ct = default);

    /// <summary>Moves to another stage and adds a Stage line; the same stage again changes nothing.</summary>
    Task<Application?> SetStageAsync(long jobId, StageRequest request, CancellationToken ct = default);

    /// <summary>Adds the owner's note, dated now.</summary>
    Task<Application?> AddNoteAsync(long jobId, NoteRequest request, CancellationToken ct = default);

    /// <summary>The numbers for the menu.</summary>
    Task<ApplicationsSummary> GetSummaryAsync(CancellationToken ct = default);
}
