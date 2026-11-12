using ReservationsHandler.Api.Data;
using ReservationsHandler.Api.Domain;
using ReservationsHandler.Api.RateLimiting;

namespace ReservationsHandler.Api.Services;

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

        // Throttle before any database work: the limiter exists to protect the database from heavy suppliers.
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
