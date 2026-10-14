using System.Text.Encodings.Web;
using System.Text.Json;
using JobFetchProbe.Models;
using JobFetchProbe.Services.Abstractions;
using Microsoft.Extensions.Options;

namespace JobFetchProbe.Infrastructure.Storage;

/// <summary>Writes each job to &lt;Output.Folder&gt;/&lt;JobId&gt;.json. Reading the same job again overwrites its file.</summary>
public sealed class JsonJobRepository(IOptions<OutputSettings> settings) : IJobRepository
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        // Keep accents and emoji readable in the file instead of \u escapes.
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    public async Task<string> SaveAsync(JobPosting job, CancellationToken ct = default)
    {
        var folder = Path.GetFullPath(settings.Value.Folder);
        Directory.CreateDirectory(folder);

        var path = Path.Combine(folder, SafeFileName(job.JobId) + ".json");
        await using var stream = File.Create(path);
        await JsonSerializer.SerializeAsync(stream, job, Json, ct);
        return path;
    }

    private static string SafeFileName(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var safe = new string(name.Select(c => invalid.Contains(c) ? '_' : c).ToArray()).Trim();
        return safe.Length > 0 ? safe : "job";
    }
}
