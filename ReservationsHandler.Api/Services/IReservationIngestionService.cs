using ReservationsHandler.Api.Domain;

namespace ReservationsHandler.Api.Services;

public interface IReservationIngestionService
{
    /// <summary>
    /// Ingests one reservation: throttles the supplier if it exceeded its rate limit, otherwise saves the
    /// reservation according to the dedup rules.
    /// </summary>
    /// <param name="reservation">An already validated reservation.</param>
    /// <param name="cancellationToken">Cancelled if the caller gives up, for example when the client disconnects.</param>
    /// <returns>
    /// Created, Updated, Duplicate or Stale when the request was processed; Throttled (with a retry delay) when the
    /// supplier is over its limit. A throttled request is an expected outcome, not an exception. Failures of the
    /// database, and cancellation, propagate as exceptions.
    /// </returns>
    Task<IngestResult> IngestAsync(Reservation reservation, CancellationToken cancellationToken = default);
}
