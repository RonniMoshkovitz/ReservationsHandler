using ReservationsHandler.Api.Data;
using ReservationsHandler.Api.Domain;
using ReservationsHandler.Api.RateLimiting;

namespace ReservationsHandler.Api.Services;

/// <summary>
/// Orchestrates ingestion: the supplier's rate limit is checked first, and only a permitted request reaches the
/// reservations table. Every request that reaches the limiter, including ones later reported as Duplicate or
/// Stale, counts against the supplier's quota.
/// </summary>
public sealed class ReservationIngestionService : IReservationIngestionService
{
    private readonly IRateLimiter _rateLimiter;
    private readonly IReservationRepository _reservations;
    private readonly ISupplierStatsRepository _stats;

    public ReservationIngestionService(
        IRateLimiter rateLimiter,
        IReservationRepository reservations,
        ISupplierStatsRepository stats)
    {
        _rateLimiter = rateLimiter;
        _reservations = reservations;
        _stats = stats;
    }

    public async Task<IngestResult> IngestAsync(Reservation reservation, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(reservation);

        // Throttle before touching the reservations table, so a heavy supplier cannot flood the dedup
        // transaction. A rejected request still costs one small counter write (the spec wants throttled
        // requests counted), so the throttle limits the expensive work but does not make rejection free.
        var rateLimit = _rateLimiter.TryAcquire(reservation.SupplierId);
        if (!rateLimit.Permitted)
        {
            await _stats.IncrementThrottledAsync(reservation.SupplierId, cancellationToken);
            return IngestResult.Throttled(rateLimit.RetryAfter);
        }

        var status = await _reservations.UpsertAsync(reservation, cancellationToken);
        return IngestResult.Processed(status);
    }
}
