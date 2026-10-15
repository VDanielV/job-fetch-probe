using System.Text.Encodings.Web;
using System.Text.Json;
using JobFetchProbe.Models;
using JobFetchProbe.Services.Abstractions;
using Microsoft.Extensions.Options;

namespace JobFetchProbe.Infrastructure.Inbox;

/// <summary>
/// Writes each batch to &lt;Inbox:OutputFolder&gt;/&lt;BatchId&gt;.json. Never overwrites: two runs in the same
/// second get "_2", "_3"…
/// </summary>
public sealed class JsonBatchRepository(IOptions<InboxSettings> settings) : IBatchRepository
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    public async Task<string> SaveAsync(JobSearchBatch batch, CancellationToken ct = default)
    {
        var folder = Path.GetFullPath(settings.Value.OutputFolder);
        Directory.CreateDirectory(folder);

        var path = Path.Combine(folder, batch.BatchId + ".json");
        for (var n = 2; File.Exists(path); n++)
        {
            path = Path.Combine(folder, $"{batch.BatchId}_{n}.json");
        }

        // The batch ID inside the file matches its name, also when "_2" was added, so the details step
        // gives each listing its own descriptions file.
        batch = batch with { BatchId = Path.GetFileNameWithoutExtension(path) };

        // CreateNew: fail rather than overwrite if another process wrote the same name in between.
        await using var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write);
        await JsonSerializer.SerializeAsync(stream, batch, Json, ct);
        return path;
    }
}
