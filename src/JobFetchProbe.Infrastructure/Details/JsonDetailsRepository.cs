using System.Text.Encodings.Web;
using System.Text.Json;
using JobFetchProbe.Models;
using JobFetchProbe.Services.Abstractions;
using Microsoft.Extensions.Options;

namespace JobFetchProbe.Infrastructure.Details;

/// <summary>
/// Writes &lt;Details:OutputFolder&gt;/&lt;BatchId&gt;.json. The file is written to a temporary name and then
/// moved over the old one, so an interrupted write never leaves a broken file.
/// </summary>
public sealed class JsonDetailsRepository(IOptions<DetailsSettings> settings) : IDetailsRepository
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    public async Task<JobDetailsBatch?> LoadAsync(string batchId, CancellationToken ct = default)
    {
        var path = PathFor(batchId);
        if (!File.Exists(path))
        {
            return null;
        }

        await using var stream = File.OpenRead(path);
        try
        {
            return await JsonSerializer.DeserializeAsync<JobDetailsBatch>(stream, Json, ct);
        }
        catch (JsonException)
        {
            return null; // unreadable: start over rather than fail
        }
    }

    public async Task<string> SaveAsync(JobDetailsBatch details, CancellationToken ct = default)
    {
        var path = PathFor(details.BatchId);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);

        var temp = path + ".tmp";
        await using (var stream = File.Create(temp))
        {
            await JsonSerializer.SerializeAsync(stream, details, Json, ct);
        }
        File.Move(temp, path, overwrite: true);
        return path;
    }

    private string PathFor(string batchId)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var safe = new string(batchId.Select(c => invalid.Contains(c) ? '_' : c).ToArray());
        return Path.Combine(Path.GetFullPath(settings.Value.OutputFolder), safe + ".json");
    }
}
