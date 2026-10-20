using JobFetchProbe.Models.Jobs;
using JobFetchProbe.Models.Journal;

namespace JobFetchProbe.Web.Pages;

/// <summary>
/// What the Acquire page shows about the latest acquisition (a step 1 run and the step 2 run after it), worked out
/// from the journal: whether it is running, both steps' figures, the recent activity and how it ended.
/// </summary>
public sealed class AcquisitionView
{
    /// <summary>Lines of recent activity shown in the progress pane.</summary>
    public const int ActivityLines = 8;

    /// <summary>Between step 1 finishing and step 2 starting, the acquisition still counts as running for this long.</summary>
    private static readonly TimeSpan BetweenSteps = TimeSpan.FromSeconds(30);

    /// <summary>After pushing the button, how long the page waits for the acquisition to appear in the journal.</summary>
    private static readonly TimeSpan StartWindow = TimeSpan.FromSeconds(60);

    public JobListingJournalRun? Listing { get; private init; }
    public JobDetailsJournalRun? Details { get; private init; }

    /// <summary>The button was pushed and the acquisition is not in the journal yet.</summary>
    public bool IsStarting { get; private init; }

    public bool IsRunning { get; private init; }

    /// <summary>The page shows the progress pane instead of the explanation pane.</summary>
    public bool ShowProgress { get; private init; }

    /// <summary>The acquisition's state as a whole: step 2's, or step 1's when step 2 never started.</summary>
    public JournalRunState? State { get; private init; }

    /// <summary>The newest lines of both steps, newest first (<see cref="ActivityLines"/> of them).</summary>
    public IReadOnlyList<ActivityLine> Activity { get; private init; } = [];

    /// <summary>Every line of both steps, oldest first: the whole run.</summary>
    public IReadOnlyList<ActivityLine> WholeRun { get; private init; } = [];

    /// <summary>How the acquisition failed, for the red pane; null unless it failed or was abandoned.</summary>
    public PageError? Error { get; private init; }

    /// <summary>How the acquisition ended, once it has.</summary>
    public PageBanner? EndBanner { get; private init; }

    public DateTimeOffset Now { get; private init; }

    /// <summary>Builds the view from the latest runs of both steps (either may be null).</summary>
    /// <param name="since">When the button was pushed, if the page follows that acquisition.</param>
    public static AcquisitionView Create(JobListingJournalRun? listing, JobDetailsJournalRun? details, DateTimeOffset? since, DateTimeOffset now)
    {
        // A step 2 run older than the step 1 run belongs to an earlier acquisition.
        if (listing is not null && details is not null && details.StartedAt < listing.StartedAt)
        {
            details = null;
        }

        // After pushing the button, older runs are not this acquisition: wait for it to appear.
        var starting = false;
        PageBanner? notStarted = null;
        if (since is { } pushed && (listing is null || listing.StartedAt < pushed.AddSeconds(-5)))
        {
            listing = null;
            details = null;
            if (now - pushed < StartWindow)
            {
                starting = true;
            }
            else
            {
                notStarted = new PageBanner("error", "The acquisition did not start",
                    "It did not appear in the journal within a minute. The API's window says why.");
            }
        }

        var running = false;
        if (listing is not null)
        {
            if (listing.State == JournalRunState.Running || details?.State == JournalRunState.Running)
            {
                running = true;
            }
            else if (listing.State == JournalRunState.Completed && details is null && listing.FinishedAt is { } done && now - done < BetweenSteps)
            {
                running = true;
            }
        }

        JournalRunState? state = null;
        if (details is not null)
        {
            state = details.State;
        }
        else if (listing is not null)
        {
            state = listing.State;
        }

        var whole = WholeRunOf(listing, details);
        PageBanner? end = notStarted;
        if (!running && !starting && state is { } final)
        {
            end = EndOf(final, listing, details);
        }

        return new AcquisitionView
        {
            Listing = listing,
            Details = details,
            IsStarting = starting,
            IsRunning = running,
            ShowProgress = running || starting || since is not null,
            State = state,
            Activity = whole.AsEnumerable().Reverse().Take(ActivityLines).ToList(),
            WholeRun = whole,
            EndBanner = end,
            Error = ErrorOf(running || starting, state, listing, details),
            Now = now,
        };
    }

    /// <summary>Saved pages archived in step 1.</summary>
    public int PagesArchived
    {
        get
        {
            if (Listing is null)
            {
                return 0;
            }
            return Listing.Entries.Count(e => e.Outcome == JobListingJournalOutcome.Archived);
        }
    }

    /// <summary>The pages step 1 kept in the input folder, with the reason for each.</summary>
    public IReadOnlyList<JobListingJournalEntry> KeptPages
    {
        get
        {
            if (Listing is null)
            {
                return [];
            }
            return Listing.Entries.Where(e => e.Outcome == JobListingJournalOutcome.Kept).ToList();
        }
    }

    /// <summary>Step 2's fit lines: scored, not scored, and whether scoring was switched off; null when the run scored nothing.</summary>
    public FitTally? Fit
    {
        get
        {
            if (Details is null)
            {
                return null;
            }
            var scored = 0;
            var notScored = 0;
            var off = false;
            foreach (var e in Details.Entries)
            {
                if (e.Kind == JobDetailsJournalEntryKind.AssessOff)
                {
                    off = true;
                }
                else if (e.Kind == JobDetailsJournalEntryKind.Assessed)
                {
                    if ((e.Message ?? string.Empty).StartsWith("Not scored", StringComparison.Ordinal))
                    {
                        notScored++;
                    }
                    else
                    {
                        scored++;
                    }
                }
            }
            if (scored == 0 && notScored == 0 && !off)
            {
                return null;
            }
            return new FitTally(scored, notScored, off);
        }
    }

    /// <summary>Why the acquisition stopped early, in a few words; null unless it was stopped.</summary>
    public string? StopReason
    {
        get
        {
            if (State != JournalRunState.Stopped)
            {
                return null;
            }
            if (Details is null)
            {
                return "during step 1; nothing was stored and the pages stay in the input folder";
            }
            var stopped = Details.Entries.LastOrDefault(e => e.Kind == JobDetailsJournalEntryKind.Stopped);
            if (stopped?.Message is { Length: > 0 } reason)
            {
                return reason;
            }
            return Details.Message;
        }
    }

    /// <summary>Step 2's time from its start to its end (or to now while it runs), cooldowns included.</summary>
    public TimeSpan? Step2Duration
    {
        get
        {
            if (Details is null)
            {
                return null;
            }
            var end = Details.FinishedAt ?? Now;
            return end - Details.StartedAt;
        }
    }

    /// <summary>When the acquisition started (step 1's start).</summary>
    public DateTimeOffset? StartedAt
    {
        get
        {
            return Listing?.StartedAt;
        }
    }

    /// <summary>The progress pane's heading.</summary>
    public string Heading
    {
        get
        {
            if (IsStarting)
            {
                return "Starting the acquisition…";
            }
            if (IsRunning)
            {
                return "Acquisition in progress";
            }
            switch (State)
            {
                case JournalRunState.Completed:
                    return "Acquisition completed";
                case JournalRunState.Stopped:
                    return "Acquisition stopped early";
                case JournalRunState.Failed:
                    return "Acquisition failed";
                case JournalRunState.Abandoned:
                    return "Acquisition ended without finishing";
                default:
                    return "Acquisition";
            }
        }
    }

    /// <summary>Step 2's jobs tried so far.</summary>
    public int JobsDone
    {
        get
        {
            if (Details is null)
            {
                return 0;
            }
            return Math.Max(0, Details.Total - Details.ToRead);
        }
    }

    /// <summary>Step 2's progress, 0 to 100.</summary>
    public int Percent
    {
        get
        {
            if (Details is null)
            {
                return 0;
            }
            if (Details.Total <= 0)
            {
                return 100;
            }
            return Math.Clamp(JobsDone * 100 / Details.Total, 0, 100);
        }
    }

    /// <summary>The circuit breaker in words, with the time left while it is open.</summary>
    public string CircuitText
    {
        get
        {
            if (Details is null)
            {
                return string.Empty;
            }
            var openings = $"opening {Details.Openings} of {Details.StopAfterOpenings}";
            switch (Details.CircuitState)
            {
                case JournalCircuitState.Open:
                    if (IsRunning && Details.CircuitOpenUntil is { } until && until > Now)
                    {
                        return $"Circuit open · trial request in {Duration(until - Now)} ({openings})";
                    }
                    return $"Circuit open ({openings})";
                case JournalCircuitState.HalfOpen:
                    return $"Trial request ({openings})";
                default:
                    return "Circuit closed";
            }
        }
    }

    /// <summary>"Running", "Completed"… for the tag next to the button.</summary>
    public string StateText
    {
        get
        {
            if (IsStarting)
            {
                return "Starting";
            }
            if (IsRunning)
            {
                return "Running";
            }
            if (State is { } state)
            {
                return state.ToString();
            }
            return string.Empty;
        }
    }

    /// <summary>A time today as "11:05", another day as "16 Oct 11:05".</summary>
    public string When(DateTimeOffset? at)
    {
        if (at is not { } value)
        {
            return string.Empty;
        }
        var local = value.ToLocalTime();
        if (local.Date == Now.ToLocalTime().Date)
        {
            return local.ToString("HH:mm");
        }
        return local.ToString("d MMM HH:mm");
    }

    /// <summary>"1h 05m", "3m 20s" or "12s".</summary>
    public static string Duration(TimeSpan t)
    {
        if (t.TotalHours >= 1)
        {
            return $"{(int)t.TotalHours}h {t.Minutes:00}m";
        }
        if (t.TotalMinutes >= 1)
        {
            return $"{t.Minutes}m {t.Seconds:00}s";
        }
        return $"{Math.Max(0, (int)Math.Ceiling(t.TotalSeconds))}s";
    }

    private static PageBanner? EndOf(JournalRunState state, JobListingJournalRun? listing, JobDetailsJournalRun? details)
    {
        var message = details?.Message ?? listing?.Message ?? string.Empty;
        switch (state)
        {
            case JournalRunState.Completed:
                return new PageBanner("ok", "Every job in the list was tried.", message);
            case JournalRunState.Stopped:
                if (details is null)
                {
                    return new PageBanner("busy", "Stopped during step 1.", "Nothing was stored; every saved page stays in the input folder for the next run.");
                }
                if (details.StopAfterOpenings > 0 && details.Openings >= details.StopAfterOpenings)
                {
                    return new PageBanner("busy", $"Stopped: the circuit breaker opened {details.Openings} time(s).",
                        "LinkedIn refused the requests. The jobs left are tried on the next run; wait an hour or more before starting again. " + message);
                }
                return new PageBanner("busy", "Stopped before the end of the list.", message);
            default:
                return null; // Failed and Abandoned: the red error pane says what happened
        }
    }

    private static PageError? ErrorOf(bool active, JournalRunState? state, JobListingJournalRun? listing, JobDetailsJournalRun? details)
    {
        if (active)
        {
            return null;
        }
        if (state == JournalRunState.Abandoned)
        {
            return PageError.Abandoned(details?.Message ?? listing?.Message);
        }
        if (state != JournalRunState.Failed)
        {
            return null;
        }
        if (details is not null && details.State == JournalRunState.Failed)
        {
            return PageError.From(details.ErrorType, details.Message);
        }
        return PageError.From(listing?.ErrorType, listing?.Message);
    }

    /// <summary>Every entry of both steps, oldest first (step 1's before step 2's).</summary>
    private static IReadOnlyList<ActivityLine> WholeRunOf(JobListingJournalRun? listing, JobDetailsJournalRun? details)
    {
        var lines = new List<ActivityLine>();
        if (listing is not null)
        {
            foreach (var e in listing.Entries)
            {
                lines.Add(ListingLine(e));
            }
        }
        if (details is not null)
        {
            foreach (var e in details.Entries)
            {
                lines.Add(DetailsLine(e));
            }
        }
        // Step 2 entries come after step 1's; within a step, ids grow in order.
        return lines.OrderBy(l => l.Step).ThenBy(l => l.Order).ToList();
    }

    private static ActivityLine ListingLine(JobListingJournalEntry e)
    {
        switch (e.Outcome)
        {
            case JobListingJournalOutcome.Read:
                return new ActivityLine(1, e.Id, e.At, "Page read", "Read", e.File, $"{e.Jobs} jobs, {e.NewJobs} new");
            case JobListingJournalOutcome.Kept:
                return new ActivityLine(1, e.Id, e.At, "Page kept", "Failed", e.File, e.Message);
            case JobListingJournalOutcome.Archived:
                return new ActivityLine(1, e.Id, e.At, "Archived", "Gone", e.File, "moved to input\\archived");
            default:
                return new ActivityLine(1, e.Id, e.At, "Not archived", "Blocked", e.File, e.Message);
        }
    }

    private static ActivityLine DetailsLine(JobDetailsJournalEntry e)
    {
        var order = 1_000_000_000L + e.Id;
        switch (e.Kind)
        {
            case JobDetailsJournalEntryKind.Job:
                if (e.Status == JobResultStatus.Read)
                {
                    return new ActivityLine(2, order, e.At, "Read", "Read", e.JobId, e.Title);
                }
                if (e.Status == JobResultStatus.Blocked)
                {
                    return new ActivityLine(2, order, e.At, "Refused", "Blocked", e.JobId, e.Message);
                }
                if (e.Gone)
                {
                    return new ActivityLine(2, order, e.At, "Gone", "Gone", e.JobId, "the job page no longer exists");
                }
                return new ActivityLine(2, order, e.At, "Failed", "Failed", e.JobId, e.Message);
            case JobDetailsJournalEntryKind.CircuitOpened:
                var wait = string.Empty;
                if (e.WaitSeconds is { } seconds)
                {
                    wait = $" · waits {Duration(TimeSpan.FromSeconds(seconds))}";
                }
                return new ActivityLine(2, order, e.At, "Circuit open", "Blocked", null, e.Message + wait);
            case JobDetailsJournalEntryKind.CircuitHalfOpen:
                return new ActivityLine(2, order, e.At, "Trial", "Pending", null, "one trial request");
            case JobDetailsJournalEntryKind.CircuitClosed:
                return new ActivityLine(2, order, e.At, "Circuit closed", "Read", null, "back to the normal pace");
            case JobDetailsJournalEntryKind.Started:
                return new ActivityLine(2, order, e.At, "Step 2", "Pending", null, e.Message);
            case JobDetailsJournalEntryKind.Assessed:
                return FitLine(e, order);
            case JobDetailsJournalEntryKind.AssessOff:
                return new ActivityLine(2, order, e.At, "Fit off", "Blocked", null, e.Message);
            case JobDetailsJournalEntryKind.Stopped:
                return new ActivityLine(2, order, e.At, "Stopped", "Blocked", null, e.Message);
            default:
                return new ActivityLine(2, order, e.At, "Finished", "Read", null, e.Message);
        }
    }

    /// <summary>The fit line under a job read: its score coloured by decision, or why it was not scored.</summary>
    private static ActivityLine FitLine(JobDetailsJournalEntry e, long order)
    {
        var text = e.Message ?? string.Empty;
        var took = string.Empty;
        if (e.WaitSeconds is { } seconds)
        {
            took = $" ({Duration(TimeSpan.FromSeconds(seconds))})";
        }
        if (text.StartsWith("Not scored", StringComparison.Ordinal))
        {
            return new ActivityLine(2, order, e.At, "Not scored", "Gone", e.JobId, text["Not scored: ".Length..] + took);
        }

        // "Fit 78 · Apply" (· hard skip): the tag carries the score, coloured like the decision.
        var tagClass = "Gone";
        if (text.Contains("· Apply", StringComparison.Ordinal))
        {
            tagClass = "Read";
        }
        else if (text.Contains("· Maybe", StringComparison.Ordinal))
        {
            tagClass = "Blocked";
        }
        var parts = text.Split(" · ", 2);
        var rest = string.Empty;
        if (parts.Length > 1)
        {
            rest = parts[1];
        }
        return new ActivityLine(2, order, e.At, parts[0], tagClass, e.JobId, rest + took);
    }
}
