namespace JobFetchProbe.Models.Filter;

/// <summary>How a job is worked, as its location text says (or does not).</summary>
public enum WorkMode
{
    NotStated,
    Remote,
    Hybrid,
    OnSite,
}
