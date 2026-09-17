namespace Ordering.Infrastructure.Catalog;

/// <summary>Options pattern for the Catalog HTTP client and its resilience pipeline (section <c>Catalog</c>).</summary>
public sealed class CatalogClientOptions
{
    public const string SectionName = "Catalog";

    /// <summary>Service discovery address: Aspire resolves <c>catalog</c>, preferring HTTPS.</summary>
    public Uri BaseAddress { get; set; } = new("https+http://catalog");

    /// <summary>Upper bound for one call including every retry.</summary>
    public TimeSpan TotalTimeout { get; set; } = TimeSpan.FromSeconds(10);

    /// <summary>Upper bound for a single attempt; a slow attempt is cancelled and retried.</summary>
    public TimeSpan AttemptTimeout { get; set; } = TimeSpan.FromSeconds(2);

    public int MaxRetryAttempts { get; set; } = 3;

    /// <summary>Base delay of the exponential backoff (jitter is added on top).</summary>
    public TimeSpan RetryBaseDelay { get; set; } = TimeSpan.FromMilliseconds(200);

    /// <summary>How long the circuit stays open (calls fail fast) before a trial call is let through.</summary>
    public TimeSpan CircuitBreakDuration { get; set; } = TimeSpan.FromSeconds(15);
}
