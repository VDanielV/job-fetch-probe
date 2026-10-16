using JobFetchProbe.Models.Jobs;
using JobFetchProbe.Models.Search;

namespace JobFetchProbe.Models.Details;

/// <summary>Reported while step 2 runs, so the console can show what is happening.</summary>
public sealed record DetailsEvent(DetailsEventKind Kind, DateTimeOffset At)
{
    /// <summary>Position (1-based) of the job just tried in this run's list.</summary>
    public int Index { get; init; }

    /// <summary>Jobs in this run's list: every job without a description.</summary>
    public int Total { get; init; }

    /// <summary>Jobs this run still has to read (at Started: all of them).</summary>
    public int ToRead { get; init; }

    public int Fetched { get; init; }
    public int Failed { get; init; }
    /// <summary>The job just tried, as its search card showed it.</summary>
    public JobListing? Job { get; init; }
    public JobResultStatus? Status { get; init; }

    /// <summary>The job page no longer exists (HTTP 404 or 410); the job is not tried again.</summary>
    public bool Gone { get; init; }
    public string? Title { get; init; }
    public string? Message { get; init; }

    /// <summary>Waiting: time left. CircuitOpened: the cooldown. Started: the pause between requests.</summary>
    public TimeSpan? Wait { get; init; }

    /// <summary>Estimated time to finish, from the average time per job so far.</summary>
    public TimeSpan? Eta { get; init; }

    public int Openings { get; init; }
    public int StopAfterOpenings { get; init; }
}
