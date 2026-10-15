using System.Text.RegularExpressions;
using JobFetchProbe.Infrastructure.Storage;
using Microsoft.Extensions.Options;

namespace JobFetchProbe.Infrastructure.Database;

/// <summary>
/// The "where it was stored" text the repositories return, e.g. "C:\…\jobs.db (listing 20261014_183201)".
/// A listing is found again from any text that contains its batch ID: this text, "20261014_183201",
/// or the name of the JSON listing file "20261014_183201.json".
/// </summary>
public sealed partial class SqliteLocations(IOptions<StorageSettings> settings)
{
    public string DatabasePath => settings.Value.Sqlite.FullPath;

    public string Job(string jobId) => $"{DatabasePath} (job {jobId})";

    public string Listing(string batchId) => $"{DatabasePath} (listing {batchId})";

    public string Search(string searchId) => $"{DatabasePath} (search {searchId})";

    /// <summary>The last batch ID in the text, or null.</summary>
    public static string? BatchIdIn(string text)
    {
        var matches = BatchId().Matches(text);
        return matches.Count > 0 ? matches[^1].Value : null;
    }

    [GeneratedRegex(@"\d{8}_\d{6}(?:_\d+)?")]
    private static partial Regex BatchId();
}
