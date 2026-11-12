using System.Collections.Concurrent;
using Microsoft.Extensions.Options;

namespace ReservationsHandler.Api.RateLimiting;

/// <summary>
/// Sliding window log: each id has a log of the timestamps of its permitted requests. A request is
/// permitted if fewer than <see cref="RateLimitOptions.PermitLimit"/> logged requests are younger than
/// <see cref="RateLimitOptions.Window"/>. Unlike a fixed window, this holds for ANY rolling window, so a burst
/// straddling a boundary cannot double the limit. Memory is bounded by the limit per id.
/// State is in memory: it is lost on restart and not shared between instances.
/// </summary>
public sealed class SlidingWindowRateLimiter : IRateLimiter
{
    private readonly ConcurrentDictionary<string, WindowLog> _logs = new(StringComparer.Ordinal);
    private readonly int _permitLimit;
    private readonly TimeSpan _window;
    private readonly TimeProvider _timeProvider;

    public SlidingWindowRateLimiter(IOptions<RateLimitOptions> options, TimeProvider timeProvider)
    {
        _permitLimit = options.Value.PermitLimit;
        _window = options.Value.Window;
        _timeProvider = timeProvider;
    }

    public RateLimitResult TryAcquire(string id)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);

        return _logs.GetOrAdd(id, static _ => new WindowLog()).TryAcquire(_permitLimit, _window, _timeProvider);
    }

    /// <summary>One id's log. All access is serialized by <see cref="_lock"/>.</summary>
    private sealed class WindowLog
    {
        private readonly object _lock = new();

        private readonly Queue<long> _timestamps = new();

        public RateLimitResult TryAcquire(int permitLimit, TimeSpan window, TimeProvider timeProvider)
        {
            lock (_lock)
            {
                // Read the clock inside the lock so the queue stays in chronological order under contention.
                var now = timeProvider.GetTimestamp();

                while (_timestamps.Count > 0 && timeProvider.GetElapsedTime(_timestamps.Peek(), now) >= window)
                {
                    _timestamps.Dequeue();
                }

                if (_timestamps.Count >= permitLimit)
                {
                    // The oldest counted request is still inside the window, so the wait is strictly positive.
                    var retryAfter = window - timeProvider.GetElapsedTime(_timestamps.Peek(), now);
                    return RateLimitResult.Reject(retryAfter);
                }

                _timestamps.Enqueue(now);
                return RateLimitResult.Permit;
            }
        }
    }
}
