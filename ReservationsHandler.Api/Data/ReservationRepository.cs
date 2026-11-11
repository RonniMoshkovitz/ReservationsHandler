using System.Collections.Frozen;
using System.Data;
using Dapper;
using ReservationsHandler.Api.Domain;

namespace ReservationsHandler.Api.Data;

public sealed class ReservationRepository : IReservationRepository
{
    private const string SelectSql = """
        SELECT SupplierId, ReservationId, RoomId, CheckIn, CheckOut, Price, UpdatedAtUtc
        FROM Reservations
        WHERE SupplierId = @SupplierId AND ReservationId = @ReservationId;
        """;

    private const string InsertSql = """
        INSERT INTO Reservations (SupplierId, ReservationId, RoomId, CheckIn, CheckOut, Price, UpdatedAtUtc)
        VALUES (@SupplierId, @ReservationId, @RoomId, @CheckIn, @CheckOut, @Price, @UpdatedAtUtc);
        """;

    private const string UpdateSql = """
        UPDATE Reservations
        SET RoomId = @RoomId, CheckIn = @CheckIn, CheckOut = @CheckOut,
            Price = @Price, UpdatedAtUtc = @UpdatedAtUtc
        WHERE SupplierId = @SupplierId AND ReservationId = @ReservationId;
        """;

    private const string IncrementIngestedSql = """
        INSERT INTO SupplierStats (SupplierId, Ingested, Throttled)
        VALUES (@SupplierId, 1, 0)
        ON CONFLICT (SupplierId) DO UPDATE SET Ingested = Ingested + 1;
        """;

    // The statements that persist a reservation, by outcome. Duplicate and Stale are deliberately absent:
    // they need no write, and are not counted as ingested.
    private static readonly FrozenDictionary<IngestStatus, string> WriteSqlByStatus =
        new Dictionary<IngestStatus, string>
        {
            [IngestStatus.Created] = InsertSql,
            [IngestStatus.Updated] = UpdateSql
        }.ToFrozenDictionary();

    private readonly IDbConnectionFactory _connectionFactory;

    public ReservationRepository(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<IngestStatus> UpsertAsync(Reservation reservation, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(reservation);

        await using var connection = await _connectionFactory.OpenConnectionAsync(cancellationToken);

        // Serializable makes the provider take the write lock up front, before the
        // SELECT. Without it two concurrent identical requests could both read "no row" and both insert.
        // Competing writers wait (up to the connection's busy timeout) instead of racing.
        await using var transaction = await connection.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);

        Task<int> ExecuteAsync(string sql) => connection.ExecuteAsync(
            new CommandDefinition(sql, reservation, transaction, cancellationToken: cancellationToken));

        var existing = await connection.QuerySingleOrDefaultAsync<Reservation>(
            new CommandDefinition(SelectSql, reservation, transaction, cancellationToken: cancellationToken));

        var status = Reservation.Evaluate(existing, reservation);

        if (!WriteSqlByStatus.TryGetValue(status, out var writeSql))
        {
            return status;
        }

        await ExecuteAsync(writeSql);
        await ExecuteAsync(IncrementIngestedSql);
        await transaction.CommitAsync(cancellationToken);
        return status;
    }
}
