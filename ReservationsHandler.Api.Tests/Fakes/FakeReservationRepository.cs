using ReservationsHandler.Api.Data;
using ReservationsHandler.Api.Domain;

namespace ReservationsHandler.Api.Tests.Fakes;

/// <summary>Returns a configured status (or throws) and records every call.</summary>
public sealed class FakeReservationRepository : IReservationRepository
{
    private readonly List<string> _callLog;

    public FakeReservationRepository(List<string>? callLog = null)
    {
        _callLog = callLog ?? [];
    }

    public IngestStatus Status { get; set; } = IngestStatus.Created;

    public Exception? ExceptionToThrow { get; set; }

    public List<Reservation> Upserted { get; } = [];

    public List<CancellationToken> Tokens { get; } = [];

    public Task<IngestStatus> UpsertAsync(Reservation reservation, CancellationToken cancellationToken = default)
    {
        _callLog.Add("repository");
        Upserted.Add(reservation);
        Tokens.Add(cancellationToken);

        return ExceptionToThrow is null
            ? Task.FromResult(Status)
            : Task.FromException<IngestStatus>(ExceptionToThrow);
    }
}
