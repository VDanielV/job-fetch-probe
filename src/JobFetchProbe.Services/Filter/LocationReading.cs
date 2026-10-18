using JobFetchProbe.Models.Filter;

namespace JobFetchProbe.Services.Filter;

/// <summary>A location text read by <see cref="LocationText"/>: the place (its key and the name shown) and the work mode.</summary>
internal sealed record LocationReading(string Key, string Label, WorkMode Mode);
