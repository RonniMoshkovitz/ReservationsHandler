using ReservationsHandler.Api.Domain;

namespace ReservationsHandler.Api.Services;

public interface IReservationIngestionService
{
    /// <summary>
    /// Ingests one reservation: throttles the supplier if it exceeded its rate limit, otherwise saves the
    /// reservation according to the dedup rules.
    /// </summary>
    /// <param name="reservation">An already validated reservation.</param>
    Task<IngestResult> IngestAsync(Reservation reservation, CancellationToken cancellationToken = default);
}
