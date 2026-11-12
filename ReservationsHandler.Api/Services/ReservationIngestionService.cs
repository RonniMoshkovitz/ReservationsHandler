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
    private readonly ILogger<ReservationIngestionService> _logger;

    public ReservationIngestionService(
        IRateLimiter rateLimiter,
        IReservationRepository reservations,
        ISupplierStatsRepository stats,
        ILogger<ReservationIngestionService> logger)
    {
        _rateLimiter = rateLimiter;
        _reservations = reservations;
        _stats = stats;
        _logger = logger;
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

            // Logged after the request was counted, and at Warning because a supplier over its limit is worth
            // noticing. Under a flood this is one line per rejected request.
            _logger.LogWarning(
                "Supplier {SupplierId} exceeded its rate limit; request rejected, retry after {RetryAfterSeconds:F1} s",
                reservation.SupplierId,
                rateLimit.RetryAfter.TotalSeconds);

            return IngestResult.Throttled(rateLimit.RetryAfter);
        }

        // A failure here is deliberately not caught and logged: it propagates to GlobalExceptionHandler, which logs
        // it once, with the stack trace. Logging it here as well would report the same failure twice.
        var status = await _reservations.UpsertAsync(reservation, cancellationToken);
        LogProcessed(status, reservation);

        return IngestResult.Processed(status);
    }

    private void LogProcessed(IngestStatus status, Reservation reservation)
    {
        if (status == IngestStatus.Stale)
        {
            // An out-of-order update is unusual enough to show in production logs.
            _logger.LogInformation(
                "Ignored stale update for reservation {ReservationId} from supplier {SupplierId}: the stored version is newer",
                reservation.ReservationId,
                reservation.SupplierId);
            return;
        }

        // Created, Updated and Duplicate are the routine outcomes: Debug, so they cost nothing in production.
        _logger.LogDebug(
            "Reservation {ReservationId} from supplier {SupplierId} was {Status}",
            reservation.ReservationId,
            reservation.SupplierId,
            status);
    }
}
