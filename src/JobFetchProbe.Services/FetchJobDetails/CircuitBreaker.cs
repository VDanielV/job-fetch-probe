namespace JobFetchProbe.Services.FetchJobDetails;

/// <summary>
/// A circuit breaker for being polite to one site. Not thread-safe: step 2 sends one request at a time.
/// Closed → (refusal, or FailureThreshold consecutive failures) → Open → (cooldown elapsed) → HalfOpen →
/// (trial succeeds) → Closed, or (trial fails) → Open with a doubled cooldown.
/// </summary>
public sealed class CircuitBreaker(CircuitBreakerOptions options, TimeProvider time)
{
    private int _consecutiveFailures;

    public CircuitState State { get; private set; } = CircuitState.Closed;

    /// <summary>How many times the circuit has opened in this run.</summary>
    public int Openings { get; private set; }

    /// <summary>When an open circuit allows the trial request.</summary>
    public DateTimeOffset OpenUntil { get; private set; }

    /// <summary>The cooldown chosen at the last opening.</summary>
    public TimeSpan LastCooldown { get; private set; }

    /// <summary>Why the circuit last opened.</summary>
    public string? Reason { get; private set; }

    /// <summary>The circuit has opened StopAfterOpenings times: the run should stop.</summary>
    public bool Exhausted => Openings >= options.StopAfterOpenings;

    public CircuitBreakerOptions Options => options;

    /// <summary>Time left before a request may be sent (zero when Closed or HalfOpen).</summary>
    public TimeSpan Remaining =>
        State == CircuitState.Open && OpenUntil > time.GetUtcNow() ? OpenUntil - time.GetUtcNow() : TimeSpan.Zero;

    /// <summary>Call before each request. Moves Open → HalfOpen once the cooldown is over.</summary>
    /// <returns>True if a request may be sent now.</returns>
    public bool TryAcquire()
    {
        if (State == CircuitState.Open && time.GetUtcNow() >= OpenUntil)
        {
            State = CircuitState.HalfOpen;
        }
        return State != CircuitState.Open;
    }

    public void RecordSuccess()
    {
        _consecutiveFailures = 0;
        State = CircuitState.Closed;
    }

    /// <summary>A refusal (401/403/429/999, sign-in page): opens the circuit at once.</summary>
    public void RecordRefusal(string reason) => Open(reason);

    /// <summary>A network failure or server error: opens the circuit after FailureThreshold in a row, or at once during the trial.</summary>
    public void RecordFailure(string reason)
    {
        _consecutiveFailures++;
        if (State == CircuitState.HalfOpen || _consecutiveFailures >= options.FailureThreshold)
        {
            Open(reason);
        }
    }

    /// <summary>A failure that says nothing about the site's health (e.g. 404 for one job): resets nothing, opens nothing.</summary>
    public void RecordNeutral()
    {
        if (State == CircuitState.HalfOpen)
        {
            // The site answered normally; that is enough to close again.
            RecordSuccess();
        }
    }

    private void Open(string reason)
    {
        Openings++;
        Reason = reason;
        _consecutiveFailures = 0;

        var factor = Math.Pow(2, Math.Min(Openings - 1, 16));
        var cooldown = TimeSpan.FromTicks((long)Math.Min(options.Cooldown.Ticks * factor, options.MaxCooldown.Ticks));
        LastCooldown = cooldown;
        OpenUntil = time.GetUtcNow() + cooldown;
        State = CircuitState.Open;
    }
}
