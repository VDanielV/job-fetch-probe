using JobFetchProbe.Models.Journal;

namespace JobFetchProbe.Services.Abstractions.RunHistory;

/// <summary>A step 2 run (without entries) and how many of the jobs it read were scored.</summary>
public sealed record DetailsRunRecord(JobDetailsJournalRun Run, int FitScored);
