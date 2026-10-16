namespace JobFetchProbe.Services.Details;

/// <summary>The state of a <see cref="CircuitBreaker"/>.</summary>
public enum CircuitState
{
    /// <summary>Normal: requests are sent.</summary>
    Closed,

    /// <summary>Tripped: no requests until the cooldown ends.</summary>
    Open,

    /// <summary>Cooldown over: exactly one trial request decides between Closed and Open.</summary>
    HalfOpen,
}
