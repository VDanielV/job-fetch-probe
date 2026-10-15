using System.Text.Json;
using System.Text.RegularExpressions;
using JobFetchProbe.Infrastructure.Inbox;
using JobFetchProbe.Models;
using JobFetchProbe.Services.Abstractions;
using Microsoft.Extensions.Options;

namespace JobFetchProbe.Infrastructure.Details;

/// <summary>Reads the inbox step's listing batches from Inbox:OutputFolder.</summary>
public sealed partial class JsonListingStore(IOptions<InboxSettings> inbox) : IListingStore
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public async Task<JobSearchBatch?> ReadAsync(string path, CancellationToken ct = default)
    {
        await using var stream = File.OpenRead(path);
        try
        {
            var batch = await JsonSerializer.DeserializeAsync<JobSearchBatch>(stream, Json, ct);
            return batch is { BatchId.Length: > 0 } ? batch : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    public string? FindLatest()
    {
        var folder = Path.GetFullPath(inbox.Value.OutputFolder);
        if (!Directory.Exists(folder))
        {
            return null;
        }

        // Batch names sort by time: yyyyMMdd_HHmmss, optionally _2, _3…
        return Directory.EnumerateFiles(folder, "*.json")
            .Where(p => BatchName().IsMatch(Path.GetFileNameWithoutExtension(p)))
            .OrderByDescending(p => Path.GetFileNameWithoutExtension(p), StringComparer.Ordinal)
            .FirstOrDefault();
    }

    public string? Resolve(string reference)
    {
        reference = reference.Trim().Trim('"');
        if (reference.Length == 0)
        {
            return null;
        }
        var path = Path.GetFullPath(reference);
        if (File.Exists(path))
        {
            return path;
        }
        // A batch ID or a listing file name: look in the listing folder.
        var name = Path.GetFileNameWithoutExtension(reference);
        if (BatchName().IsMatch(name))
        {
            var inFolder = Path.Combine(Path.GetFullPath(inbox.Value.OutputFolder), name + ".json");
            if (File.Exists(inFolder))
            {
                return inFolder;
            }
        }
        return null;
    }

    [GeneratedRegex(@"^\d{8}_\d{6}(_\d+)?$")]
    private static partial Regex BatchName();
}
