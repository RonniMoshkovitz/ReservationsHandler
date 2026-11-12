using ReservationsHandler.Api.RateLimiting;

namespace ReservationsHandler.Api.Tests.Fakes;

/// <summary>Returns a configured result and records the ids it was asked about.</summary>
public sealed class FakeRateLimiter : IRateLimiter
{
    private readonly List<string> _callLog;

    /// <param name="callLog">Shared, ordered log so a test can assert which collaborator was called first.</param>
    public FakeRateLimiter(List<string>? callLog = null)
    {
        _callLog = callLog ?? [];
    }

    public RateLimitResult Result { get; set; } = RateLimitResult.Permit;

    public List<string> RequestedIds { get; } = [];

    public RateLimitResult TryAcquire(string id)
    {
        _callLog.Add("limiter");
        RequestedIds.Add(id);
        return Result;
    }
}
