using ReservationsHandler.Api.Domain;

namespace ReservationsHandler.Api.Data;

public interface IReservationRepository
{
    /// <summary>
    /// Saves the reservation according to the dedup rules (see <see cref="Reservation.Evaluate"/>),
    /// atomically. When the result is <see cref="IngestStatus.Created"/> or <see cref="IngestStatus.Updated"/>
    /// the supplier's ingested counter is incremented in the same transaction. Duplicate and stale
    /// requests write nothing. Never returns <see cref="IngestStatus.Throttled"/>.
    /// </summary>
    Task<IngestStatus> UpsertAsync(Reservation reservation, CancellationToken cancellationToken = default);
}
