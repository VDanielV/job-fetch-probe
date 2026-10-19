namespace JobFetchProbe.Models.Filter;

/// <summary>The order of the Overview: newest job IDs first, or the best latest fit first (jobs never assessed last).</summary>
public enum JobSort
{
    Newest,
    Fit,
}
