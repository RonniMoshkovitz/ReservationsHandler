using ReservationsHandler.Api.Data;
using ReservationsHandler.Api.Domain;

namespace ReservationsHandler.Api.Tests.Fakes;

/// <summary>Returns a configured status and records what it was asked to save.</summary>
public sealed class FakeReservationRepository : IReservationRepository
{
    public IngestStatus Status { get; set; } = IngestStatus.Created;

    public List<Reservation> Upserted { get; } = [];

    public Task<IngestStatus> UpsertAsync(Reservation reservation, CancellationToken cancellationToken = default)
    {
        Upserted.Add(reservation);

        return Task.FromResult(Status);
    }
}
