using Dapper;

namespace ReservationsHandler.Api.Data;

/// <summary>Creates the schema if it does not exist. Idempotent, so it is safe to run on every startup.</summary>
public sealed class DatabaseInitializer
{
    // Dates are ISO-8601 UTC text and prices are decimal text (see the type handlers). The default BINARY
    // collation keeps supplier and reservation ids case-sensitive.
    private const string Schema = """
        CREATE TABLE IF NOT EXISTS Reservations (
            SupplierId    TEXT NOT NULL,
            ReservationId TEXT NOT NULL,
            RoomId        TEXT NOT NULL,
            CheckIn       TEXT NOT NULL,
            CheckOut      TEXT NOT NULL,
            Price         TEXT NOT NULL,
            UpdatedAtUtc  TEXT NOT NULL,
            PRIMARY KEY (SupplierId, ReservationId)
        ) WITHOUT ROWID;

        CREATE TABLE IF NOT EXISTS SupplierStats (
            SupplierId TEXT    NOT NULL PRIMARY KEY,
            Ingested   INTEGER NOT NULL DEFAULT 0,
            Throttled  INTEGER NOT NULL DEFAULT 0
        ) WITHOUT ROWID;
        """;

    private readonly IDbConnectionFactory _connectionFactory;

    public DatabaseInitializer(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = await _connectionFactory.OpenConnectionAsync(cancellationToken);
        await connection.ExecuteAsync(new CommandDefinition(Schema, cancellationToken: cancellationToken));
    }
}
