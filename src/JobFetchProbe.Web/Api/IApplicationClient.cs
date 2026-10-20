using JobFetchProbe.Models.Applications;

namespace JobFetchProbe.Web.Api;

/// <summary>
/// The calls the web app makes to the API's Applications controller. Throws <see cref="ApiUnavailableException"/> when the
/// API is not reachable, and <see cref="ApiErrorException"/> with the API's message when it refuses the request.
/// </summary>
public interface IApplicationClient
{
    /// <summary>POST api/Applications: "I applied".</summary>
    Task<Application> ApplyAsync(long jobId, CancellationToken ct = default);

    /// <summary>
    /// DELETE api/Applications/{jobId}: undo. Returns 0 when it was deleted; otherwise the number of notes the owner wrote,
    /// and nothing was deleted (ask, then call again with <paramref name="withNotes"/>).
    /// </summary>
    Task<int> UndoAsync(long jobId, bool withNotes, CancellationToken ct = default);

    /// <summary>GET api/Applications: one page of the open (or closed) applications.</summary>
    Task<ApplicationPage> GetPageAsync(
        bool closed, ApplicationStage? stage, bool flagged, int page, int pageSize, ApplicationSort sort = ApplicationSort.FollowUp,
        CancellationToken ct = default);

    /// <summary>GET api/Applications/{jobId}, or null when the job is not in the applications.</summary>
    Task<Application?> GetAsync(long jobId, CancellationToken ct = default);

    /// <summary>PUT api/Applications/{jobId}/stage.</summary>
    Task<Application> SetStageAsync(long jobId, ApplicationStage stage, CancellationToken ct = default);

    /// <summary>POST api/Applications/{jobId}/notes.</summary>
    Task<Application> AddNoteAsync(long jobId, string text, CancellationToken ct = default);

    /// <summary>GET api/Applications/summary: the numbers for the menu.</summary>
    Task<ApplicationsSummary> GetSummaryAsync(CancellationToken ct = default);
}
