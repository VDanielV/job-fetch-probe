using System.Text.Encodings.Web;
using System.Text.Json;
using JobFetchProbe.Models;
using JobFetchProbe.Services.Abstractions;
using Microsoft.Extensions.Options;

namespace JobFetchProbe.Infrastructure.Search;

/// <summary>Writes each search to &lt;Search.OutputFolder&gt;/&lt;SearchId&gt;.json. Reading the same search again overwrites its file.</summary>
public sealed class JsonSearchRepository(IOptions<SearchSettings> settings) : ISearchRepository
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    public async Task<string> SaveAsync(JobSearch search, CancellationToken ct = default)
    {
        var folder = Path.GetFullPath(settings.Value.OutputFolder);
        Directory.CreateDirectory(folder);

        var path = Path.Combine(folder, SafeFileName(search.SearchId) + ".json");
        await using var stream = File.Create(path);
        await JsonSerializer.SerializeAsync(stream, search, Json, ct);
        return path;
    }

    private static string SafeFileName(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var safe = new string(name.Select(c => invalid.Contains(c) ? '_' : c).ToArray()).Trim();
        return safe.Length > 0 ? safe : "search";
    }
}
