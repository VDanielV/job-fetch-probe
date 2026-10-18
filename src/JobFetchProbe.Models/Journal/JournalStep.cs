namespace JobFetchProbe.Models.Journal;

/// <summary>Which step a journal run belongs to.</summary>
public enum JournalStep
{
    /// <summary>Step 1, FetchJobListing: saved search pages → job listing.</summary>
    JobListing,

    /// <summary>Step 2, FetchJobDetails: job listing → job details.</summary>
    JobDetails,
}
