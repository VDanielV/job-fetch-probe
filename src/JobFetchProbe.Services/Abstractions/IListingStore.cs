using JobFetchProbe.Models;

namespace JobFetchProbe.Services.Abstractions;

/// <summary>Reads listing batches (the inbox step's &lt;yyyyMMdd_HHmmss&gt;.json files).</summary>
public interface IListingStore
{
    /// <summary>Reads a listing (a file path with JSON storage). Returns null if it is not a listing batch.</summary>
    Task<JobSearchBatch?> ReadAsync(string path, CancellationToken ct = default);

    /// <summary>The newest listing, or null if there is none.</summary>
    string? FindLatest();

    /// <summary>
    /// Finds a listing from what the user typed: a listing file path, a batch ID ("20261014_183201") or a
    /// listing file name ("20261014_183201.json"). Returns the value to pass to <see cref="ReadAsync"/>, or null if
    /// there is no such listing.
    /// </summary>
    string? Resolve(string reference);
}
