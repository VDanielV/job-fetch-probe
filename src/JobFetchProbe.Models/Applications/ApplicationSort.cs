namespace JobFetchProbe.Models.Applications;

/// <summary>The order of the Applications list: flagged first (the default), or the best latest fit first.</summary>
public enum ApplicationSort
{
    FollowUp,
    Fit,
}
