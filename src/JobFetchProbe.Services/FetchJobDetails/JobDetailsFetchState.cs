using JobFetchProbe.Models.FetchJobDetails;
using JobFetchProbe.Models.Search;

namespace JobFetchProbe.Services.FetchJobDetails;

/// <summary>Everything one step 2 run keeps track of while it goes through its work list.</summary>
internal sealed class JobDetailsFetchState(
    IReadOnlyList<JobListing> unread,
    CircuitBreaker breaker,
    IProgress<JobDetailsFetchEvent>? progress,
    TimeProvider time)
{
    public const string CancelledReason = "cancelled by the user";

    /// <summary>This run's work list: every job without a description, as it was at the start.</summary>
    public IReadOnlyList<JobListing> Unread { get; } = unread;

    public CircuitBreaker Breaker { get; } = breaker;

    public TimeLeftEstimate Clock { get; } = new();

    public int Fetched { get; set; }

    /// <summary>Jobs that failed in this run and were not read later in it.</summary>
    public HashSet<string> Failed { get; } = new(StringComparer.Ordinal);

    /// <summary>Distinct jobs tried in this run.</summary>
    public HashSet<string> Attempted { get; } = new(StringComparer.Ordinal);

    public int Requests { get; set; }

    /// <summary>Why the run stopped early; null while it goes on.</summary>
    public string? Stopped { get; set; }

    /// <summary>Jobs in the work list not tried yet.</summary>
    public int Left
    {
        get
        {
            return Unread.Count - Attempted.Count;
        }
    }

    /// <summary>Reports an event of <paramref name="kind"/>, filled in by <paramref name="fill"/>.</summary>
    public void Report(JobDetailsFetchEventKind kind, Func<JobDetailsFetchEvent, JobDetailsFetchEvent> fill)
    {
        progress?.Report(fill(new JobDetailsFetchEvent(kind, time.GetLocalNow())));
    }
}
