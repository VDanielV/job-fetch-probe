namespace JobFetchProbe.Models.Journal;

/// <summary>What happened to one saved page in a step 1 run.</summary>
public enum JobListingJournalOutcome
{
    /// <summary>Read, with at least one job.</summary>
    Read,

    /// <summary>Could not be read; it stays in the input folder.</summary>
    Kept,

    /// <summary>Read and moved to the archive folder.</summary>
    Archived,

    /// <summary>Read, but could not be moved to the archive folder.</summary>
    NotArchived,
}
