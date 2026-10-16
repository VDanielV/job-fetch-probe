namespace JobFetchProbe.Data.Entities;

/// <summary>Values of <see cref="JobRow.Status"/>.</summary>
public static class JobStatus
{
    /// <summary>Listed, job page not read yet.</summary>
    public const string Pending = "Pending";

    /// <summary>Job page read; the description is stored.</summary>
    public const string Read = "Read";

    /// <summary>The site refused the last attempt. Tried again on the next run.</summary>
    public const string Blocked = "Blocked";

    /// <summary>The last attempt failed (network, server error, nothing readable). Tried again on the next run.</summary>
    public const string Failed = "Failed";

    /// <summary>The job page no longer exists (HTTP 404 or 410). Not tried again.</summary>
    public const string Gone = "Gone";

    /// <summary>The statuses step 2 tries: everything without a description that may still exist.</summary>
    public static readonly string[] ToRead = [Pending, Failed, Blocked];
}
