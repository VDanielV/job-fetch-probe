namespace JobFetchProbe.Services.Journal;

/// <summary>Rules of the journal.</summary>
/// <param name="AbandonedAfter">A run with no sign of life for this long is reported as Abandoned.</param>
/// <param name="SignOfLifeEvery">How often a running process confirms it is alive (well below AbandonedAfter).</param>
/// <param name="KeepRunsPerStep">Runs kept per step; older ones are deleted when a new run starts.</param>
public sealed record JournalOptions(TimeSpan AbandonedAfter, TimeSpan SignOfLifeEvery, int KeepRunsPerStep)
{
    public static JournalOptions Default { get; } = new(TimeSpan.FromMinutes(2), TimeSpan.FromSeconds(30), 50);
}
