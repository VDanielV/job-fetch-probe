using JobFetchProbe.Models.Journal;

namespace JobFetchProbe.Models.RunHistory;

/// <summary>
/// One past acquisition: a step 1 run with the step 2 run that followed it, or a listing stored before the journal
/// existed (then <see cref="RunId"/> is null and only the job links are known).
/// </summary>
/// <param name="RunId">The step 1 run's journal ID: the run's address (/runs/{RunId}); null for a listing without a journal.</param>
/// <param name="DetailsRunId">The step 2 run's journal ID; null when step 2 did not run (or its journal was pruned).</param>
/// <param name="ListingId">The listing step 1 stored; null when it stored none.</param>
/// <param name="State">How the acquisition ended (step 2's state, or step 1's when step 2 did not run); null for a listing without a journal.</param>
/// <param name="Links">Job links in the listing.</param>
/// <param name="New">Of those, jobs first seen in this run.</param>
/// <param name="Known">Of those, jobs already in the database before this run.</param>
/// <param name="Read">Descriptions read by step 2.</param>
/// <param name="Failed">Descriptions step 2 could not read.</param>
/// <param name="FitScored">Jobs read and scored in this run.</param>
/// <param name="Openings">Times the circuit breaker opened.</param>
/// <param name="Empty">No saved page and nothing tried by step 2.</param>
public sealed record RunHistoryLine(
    long? RunId,
    long? DetailsRunId,
    string? ListingId,
    DateTimeOffset StartedAt,
    DateTimeOffset? FinishedAt,
    JournalRunState? State,
    int Pages,
    int PagesRead,
    int Links,
    int New,
    int Known,
    int Read,
    int Failed,
    int FitScored,
    int Openings,
    string? Message,
    bool Empty);
