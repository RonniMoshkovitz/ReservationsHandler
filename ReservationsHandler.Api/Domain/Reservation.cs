namespace ReservationsHandler.Api.Domain;

/// <summary>
/// A supplier's reservation. Identity is the composite (<see cref="SupplierId"/>, <see cref="ReservationId"/>):
/// reservation ids are issued by each supplier independently, so they are only unique per supplier.
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

    /// <summary>
    /// The dedup rules: decides what <paramref name="incoming"/> means relative to what is stored.
    /// Pure (no I/O), so the rules can be tested without a database; the repository only has to
    /// perform the write that the returned status implies.
    /// </summary>
    /// <param name="existing">The stored reservation with the same identity, or null if there is none.</param>
    /// <param name="incoming">The reservation that just arrived.</param>
    /// <returns>
    /// <see cref="IngestStatus.Created"/> when nothing is stored yet;
    /// <see cref="IngestStatus.Duplicate"/> when every detail is identical (no write needed);
    /// <see cref="IngestStatus.Stale"/> when the incoming update is older than the stored one (no write);
    /// otherwise <see cref="IngestStatus.Updated"/>. Never <see cref="IngestStatus.Throttled"/>.
    /// </returns>
    public static IngestStatus Evaluate(Reservation? existing, Reservation incoming)
    {
        ArgumentNullException.ThrowIfNull(incoming);

        if (existing is null)
        {
            return IngestStatus.Created;
        }

        if (existing.SameDetailsAs(incoming))
        {
            return IngestStatus.Duplicate;
        }

        // An equal updatedAtUtc with other details changed is treated as an update (not stale).
        return incoming.UpdatedAtUtc < existing.UpdatedAtUtc
            ? IngestStatus.Stale
            : IngestStatus.Updated;
    }
}
