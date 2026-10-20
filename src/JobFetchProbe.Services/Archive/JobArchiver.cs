using JobFetchProbe.Models.Archive;
using JobFetchProbe.Services.Abstractions;
using JobFetchProbe.Services.Abstractions.Archive;
using JobFetchProbe.Services.Catalog;

namespace JobFetchProbe.Services.Archive;

/// <summary>The rules for archiving; the data layer records it (<see cref="IJobArchiveRepository"/>).</summary>
public sealed class JobArchiver(IJobArchiveRepository archive, IJobCatalog catalog, TimeProvider time) : IJobArchiver
{
    public const int MaxJobs = 100;
    public const int MaxNoteLength = 500;

    public async Task<ArchiveResult> ArchiveAsync(ArchiveRequest request, CancellationToken ct = default)
    {
        var ids = (request.JobIds ?? [])
            .Where(id => id > 0)
            .Distinct()
            .ToList();

        if (ids.Count == 0)
        {
            throw new ArchiveRequestException("jobIds", "Choose at least one job to archive.");
        }
        if (ids.Count > MaxJobs)
        {
            throw new ArchiveRequestException("jobIds", $"At most {MaxJobs} jobs can be archived at once.");
        }
        if (!Enum.IsDefined(request.Reason))
        {
            throw new ArchiveRequestException("reason", "Unknown reason.");
        }

        string? note = null;
        if (!string.IsNullOrWhiteSpace(request.Note))
        {
            note = request.Note.Trim();
        }
        if (note is not null && note.Length > MaxNoteLength)
        {
            throw new ArchiveRequestException("note", $"The note can have at most {MaxNoteLength} characters.");
        }
        if (request.Reason == ArchiveReason.Other && note is null)
        {
            throw new ArchiveRequestException("note", "With the reason Other, write a note saying why.");
        }

        if (!catalog.IsAvailable)
        {
            throw new JobStoreUnavailableException();
        }
        return await archive.ArchiveAsync(ids, request.Reason, note, time.GetLocalNow(), ct);
    }
}
