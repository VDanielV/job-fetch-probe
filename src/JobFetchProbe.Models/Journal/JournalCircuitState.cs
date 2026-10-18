namespace JobFetchProbe.Models.Journal;

/// <summary>The circuit breaker as last reported by step 2.</summary>
public enum JournalCircuitState
{
    Closed,
    Open,
    HalfOpen,
}
