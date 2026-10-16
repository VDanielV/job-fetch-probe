namespace JobFetchProbe.Models.Jobs;

/// <summary>Outcome of reading one page.</summary>
public enum JobResultStatus
{
    /// <summary>The page was read.</summary>
    Read,

    /// <summary>The site refused the request or asked for a sign-in.</summary>
    Blocked,

    /// <summary>Bad input, transport failure, unexpected HTTP status, or nothing readable in the page.</summary>
    Failed,
}
