namespace ReservationsHandler.Api.RateLimiting;

/// <summary>The outcome of asking the limiter for permission to process one request.</summary>
/// <param name="Permitted">True when the request may proceed.</param>
/// <param name="RetryAfter">When rejected, how long until a request would be permitted again; otherwise zero.</param>
public readonly record struct RateLimitResult(bool Permitted, TimeSpan RetryAfter)
{
    public static RateLimitResult Permit { get; } = new(true, TimeSpan.Zero);

    public static RateLimitResult Reject(TimeSpan retryAfter) => new(false, retryAfter);
}
