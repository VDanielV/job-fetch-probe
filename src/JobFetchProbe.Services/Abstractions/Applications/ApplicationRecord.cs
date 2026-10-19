using JobFetchProbe.Models.Applications;
using JobFetchProbe.Models.Fit;

namespace JobFetchProbe.Services.Abstractions.Applications;

/// <summary>An application as stored, with what the list shows of its job and of its notes. The flags are added by the services.</summary>
/// <param name="LastNote">The owner's most recent note, or null.</param>
/// <param name="NoteCount">How many notes the owner wrote.</param>
/// <param name="Fit">The job's latest fit assessment, or null.</param>
public sealed record ApplicationRecord(
    string JobId,
    string? Title,
    string? Company,
    string? Location,
    ApplicationStage Stage,
    DateTimeOffset AppliedAt,
    DateTimeOffset StageChangedAt,
    string? LastNote,
    int NoteCount,
    FitScore? Fit = null);
