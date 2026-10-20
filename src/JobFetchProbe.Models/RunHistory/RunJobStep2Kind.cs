namespace JobFetchProbe.Models.RunHistory;

/// <summary>What step 2 did, in one run, with one job of that run's listing.</summary>
public enum RunJobStep2Kind
{
    /// <summary>Its description was read.</summary>
    Read,
    /// <summary>The site refused the request (sign-in or refusal page); tried again on a later run.</summary>
    Refused,
    /// <summary>The page could not be fetched.</summary>
    Failed,
    /// <summary>The page no longer exists.</summary>
    Gone,
    /// <summary>Not requested, on purpose: already read, archived, applied to, gone, or from Notion.</summary>
    NotAsked,
    /// <summary>Waiting to be read, but the run ended before it (stopped, failed, or step 2 did not run).</summary>
    NotReached,
    /// <summary>The run has no journal (stored before the journal existed, or pruned): what step 2 did is not known.</summary>
    Unknown,
}
