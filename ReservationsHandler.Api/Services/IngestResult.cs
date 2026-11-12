using ReservationsHandler.Api.Domain;

namespace ReservationsHandler.Api.Services;

/// <summary>
/// The outcome of ingesting one reservation. A throttled request is an expected outcome, not an error,
/// so it is a result and not an exception; it is the only outcome that carries a <see cref="RetryAfter"/>.
/// </summary>
public sealed record IngestResult
{
    public IngestStatus Status { get; }

    /// <summary>How long until the supplier may retry. Set only when <see cref="Status"/> is Throttled.</summary>
    public TimeSpan? RetryAfter { get; }

    private IngestResult(IngestStatus status, TimeSpan? retryAfter)
    {
        Status = status;
        RetryAfter = retryAfter;
    }

    /// <summary>The request was processed by the repository: Created, Updated, Duplicate or Stale.</summary>
    /// <exception cref="ArgumentException"><paramref name="status"/> is Throttled, use <see cref="Throttled"/>.</exception>
    public static IngestResult Processed(IngestStatus status) => status == IngestStatus.Throttled
        ? throw new ArgumentException($"Use {nameof(Throttled)} for a throttled request.", nameof(status))
        : new IngestResult(status, retryAfter: null);

    /// <summary>The supplier exceeded its rate limit, <paramref name="retryAfter"/> is how long until it may retry.</summary>
    public static IngestResult Throttled(TimeSpan retryAfter) => new(IngestStatus.Throttled, retryAfter);
}
