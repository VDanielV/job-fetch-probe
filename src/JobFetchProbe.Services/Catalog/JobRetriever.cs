using JobFetchProbe.Models.Catalog;
using JobFetchProbe.Models.Filter;
using JobFetchProbe.Services.Abstractions;
using JobFetchProbe.Services.Filter;

namespace JobFetchProbe.Services.Catalog;

/// <summary>
/// The rules for reading stored jobs. The data comes from <see cref="IJobCatalog"/>, implemented by the data layer,
/// which also says whether the database exists.
/// </summary>
public sealed class JobRetriever(IJobCatalog catalog) : IJobRetriever
{
    public const int DefaultPageSize = 25;
    public const int MaxPageSize = 100;

    /// <summary>How many city chips the Overview shows before "Other".</summary>
    public const int PlaceChips = 10;
    public const string OtherPlacesKey = "other";

    public async Task<JobSummaryPage> GetPageAsync(
        int page, int pageSize, bool includeArchived = false, string? query = null, FilterScope scope = FilterScope.All,
        IReadOnlyList<string>? places = null, IReadOnlyList<string>? modes = null, CancellationToken ct = default)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(page, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(pageSize, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(pageSize, MaxPageSize);
        EnsureAvailable();

        // Every listed job is read and matched here: the text filter ignores accents (SQLite cannot), and the city and
        // work-mode chips need their counts.
        var candidates = await catalog.GetFilterCandidatesAsync(includeArchived, ct);
        var filter = TextFilter.Parse(query);
        var searchDescription = scope == FilterScope.All;
        var jobs = candidates.Jobs
            .Select(c => new Listed(
                c,
                LocationText.Read(c.Summary.Location),
                filter.IsEmpty || TextFilter.Matches(filter, c.Summary.Title, c.Description, searchDescription)))
            .ToList();

        // The chips: the places with the most jobs (whatever is filtered), "Spain, no city" always, the rest as Other.
        var ranked = jobs
            .GroupBy(j => j.Location.Key)
            .Select(g => (Key: g.Key, Label: g.First().Location.Label, Count: g.Count()))
            .OrderByDescending(g => g.Count)
            .ThenBy(g => g.Label, StringComparer.CurrentCulture)
            .ToList();
        var shown = ranked.Take(PlaceChips).ToList();
        var countryOnly = ranked.FirstOrDefault(r => r.Key == LocationText.CountryOnlyKey);
        if (countryOnly.Key is not null && !shown.Any(r => r.Key == LocationText.CountryOnlyKey))
        {
            shown.Add(countryOnly);
        }
        var shownKeys = shown.Select(r => r.Key).ToHashSet(StringComparer.Ordinal);
        string ChipOf(Listed job)
        {
            if (shownKeys.Contains(job.Location.Key))
            {
                return job.Location.Key;
            }
            return OtherPlacesKey;
        }

        // Unknown keys (a place no longer among the chips, a typo in the address) are ignored, as unknown modes are.
        var placeSet = (places ?? [])
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .Select(p => p.Trim().ToLowerInvariant())
            .Where(p => shownKeys.Contains(p) || p == OtherPlacesKey)
            .ToHashSet(StringComparer.Ordinal);
        var modeSet = new HashSet<WorkMode>();
        foreach (var key in modes ?? [])
        {
            if (LocationText.TryParseMode(key, out var mode))
            {
                modeSet.Add(mode);
            }
        }
        bool PlaceOk(Listed job)
        {
            return placeSet.Count == 0 || placeSet.Contains(ChipOf(job));
        }
        bool ModeOk(Listed job)
        {
            return modeSet.Count == 0 || modeSet.Contains(job.Location.Mode);
        }

        // A chip counts the jobs it would list together with the other filters (the text, and the other row).
        var placeCounts = jobs.Where(j => j.TextMatches && ModeOk(j)).GroupBy(ChipOf).ToDictionary(g => g.Key, g => g.Count());
        var modeCounts = jobs.Where(j => j.TextMatches && PlaceOk(j)).GroupBy(j => j.Location.Mode).ToDictionary(g => g.Key, g => g.Count());
        var placeChips = shown
            .Select(r => new FilterChip(r.Key, r.Label, placeCounts.GetValueOrDefault(r.Key), placeSet.Contains(r.Key)))
            .ToList();
        if (jobs.Any(j => ChipOf(j) == OtherPlacesKey))
        {
            placeChips.Add(new FilterChip(OtherPlacesKey, "Other", placeCounts.GetValueOrDefault(OtherPlacesKey), placeSet.Contains(OtherPlacesKey)));
        }
        var modeChips = new[] { WorkMode.Remote, WorkMode.Hybrid, WorkMode.OnSite, WorkMode.NotStated }
            .Select(m => new FilterChip(LocationText.KeyOf(m), LocationText.LabelOf(m), modeCounts.GetValueOrDefault(m), modeSet.Contains(m)))
            .ToList();

        var matches = jobs.Where(j => j.TextMatches && PlaceOk(j) && ModeOk(j)).ToList();
        var pages = Math.Max(1, (matches.Count + pageSize - 1) / pageSize);
        page = Math.Clamp(page, 1, pages);
        var rows = new List<JobSummary>();
        foreach (var match in matches.Skip((page - 1) * pageSize).Take(pageSize))
        {
            string? snippet = null;
            if (!filter.IsEmpty && searchDescription)
            {
                snippet = TextFilter.Snippet(filter, match.Candidate.Summary.Title, match.Candidate.Description);
            }
            rows.Add(match.Candidate.Summary with { Snippet = snippet, Place = match.Location.Label, WorkMode = match.Location.Mode });
        }

        var filtering = !filter.IsEmpty || placeSet.Count > 0 || modeSet.Count > 0;
        IReadOnlyList<string>? terms = null;
        if (!filter.IsEmpty)
        {
            terms = filter.Include;
        }
        int? unfiltered = null;
        if (filtering)
        {
            unfiltered = jobs.Count;
        }
        return new JobSummaryPage(rows, page, pageSize, matches.Count, candidates.ArchivedCount, terms, unfiltered, placeChips, modeChips);
    }

    public async Task<StoredJob?> GetJobAsync(string jobId, CancellationToken ct = default)
    {
        EnsureAvailable();
        var id = jobId?.Trim();
        return string.IsNullOrEmpty(id) ? null : await catalog.GetAsync(id, ct);
    }

    private void EnsureAvailable()
    {
        if (!catalog.IsAvailable)
        {
            throw new JobStoreUnavailableException();
        }
    }
}
