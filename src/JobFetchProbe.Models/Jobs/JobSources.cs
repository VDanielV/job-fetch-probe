namespace JobFetchProbe.Models.Jobs;

/// <summary>Where a job comes from: the value of its Source. Together with its SourceJobId it is unique.</summary>
public static class JobSources
{
    /// <summary>LinkedIn: the SourceJobId is the number in /jobs/view/…</summary>
    public const string LinkedIn = "linkedin";

    /// <summary>The one-time Notion import: the SourceJobId is the imported ID, e.g. Notion-A027 or InfoJobs-A071.</summary>
    public const string Notion = "notion";
}
