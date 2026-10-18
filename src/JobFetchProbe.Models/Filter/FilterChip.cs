namespace JobFetchProbe.Models.Filter;

/// <summary>One chip of the Overview's location or work-mode row.</summary>
/// <param name="Key">The value in the address, e.g. "valencia" or "hybrid".</param>
/// <param name="Count">Jobs it would list together with the other filters now on.</param>
public sealed record FilterChip(string Key, string Label, int Count, bool Selected);
