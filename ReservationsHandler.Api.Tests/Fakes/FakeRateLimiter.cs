using ReservationsHandler.Api.RateLimiting;

namespace ReservationsHandler.Api.Tests.Fakes;

/// <summary>Answers every request with a configured result.</summary>
public sealed class FakeRateLimiter : IRateLimiter
{
    public RateLimitResult Result { get; set; } = RateLimitResult.Permit;

    public RateLimitResult TryAcquire(string id) => Result;
}
