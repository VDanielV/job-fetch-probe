using JobFetchProbe.Models.Filter;
using JobFetchProbe.Services.Filter;

namespace JobFetchProbe.Services.Catalog;

/// <summary>A listed job while <see cref="JobRetriever"/> filters: its location read, and whether the text filter matched.</summary>
internal sealed record Listed(FilterCandidate Candidate, LocationReading Location, bool TextMatches);
