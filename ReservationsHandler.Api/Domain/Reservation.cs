namespace ReservationsHandler.Api.Domain;

/// <summary>
/// A supplier's reservation. Identity is the composite (<see cref="SupplierId"/>, <see cref="ReservationId"/>):
/// reservation ids are issued by each supplier independently, so they are only unique per supplier.
/// All timestamps are expected to be UTC.
/// </summary>
public sealed record Reservation(
    string SupplierId,
    string ReservationId,
    string RoomId,
    DateTimeOffset CheckIn,
    DateTimeOffset CheckOut,
    decimal Price,
    DateTimeOffset UpdatedAtUtc)
{
    /// <summary>
    /// True when every detail (everything except the identity) matches <paramref name="other"/>.
    /// This is what decides between <see cref="IngestStatus.Duplicate"/> and an update.
    /// </summary>
    public bool SameDetailsAs(Reservation other)
    {
        ArgumentNullException.ThrowIfNull(other);

        return RoomId == other.RoomId
               && CheckIn == other.CheckIn
               && CheckOut == other.CheckOut
               && Price == other.Price
               && UpdatedAtUtc == other.UpdatedAtUtc;
    }
}
