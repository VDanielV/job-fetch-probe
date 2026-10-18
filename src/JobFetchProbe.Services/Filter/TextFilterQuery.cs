namespace JobFetchProbe.Services.Filter;

/// <summary>The terms of a text filter: those that must appear, and those that must not.</summary>
internal sealed record TextFilterQuery(IReadOnlyList<string> Include, IReadOnlyList<string> Exclude)
{
    public bool IsEmpty
    {
        get
        {
            return Include.Count == 0 && Exclude.Count == 0;
        }
    }
}
