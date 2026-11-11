using Dapper;
using ReservationsHandler.Api.Domain;

namespace ReservationsHandler.Api.Data;

public sealed class SupplierStatsRepository : ISupplierStatsRepository
{
    // A single statement, so it is atomic without an explicit transaction.
    private const string IncrementThrottledSql = """
        INSERT INTO SupplierStats (SupplierId, Ingested, Throttled)
        VALUES (@supplierId, 0, 1)
        ON CONFLICT (SupplierId) DO UPDATE SET Throttled = Throttled + 1;
        """;

    private const string SelectSql = """
        SELECT SupplierId, Ingested, Throttled FROM SupplierStats WHERE SupplierId = @supplierId;
        """;

    private readonly IDbConnectionFactory _connectionFactory;

    public SupplierStatsRepository(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task IncrementThrottledAsync(string supplierId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(supplierId);

        await using var connection = await _connectionFactory.OpenConnectionAsync(cancellationToken);
        await connection.ExecuteAsync(
            new CommandDefinition(IncrementThrottledSql, new { supplierId }, cancellationToken: cancellationToken));
    }

    public async Task<SupplierStats> GetAsync(string supplierId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(supplierId);

        await using var connection = await _connectionFactory.OpenConnectionAsync(cancellationToken);
        var stats = await connection.QuerySingleOrDefaultAsync<SupplierStats>(
            new CommandDefinition(SelectSql, new { supplierId }, cancellationToken: cancellationToken));

        return stats ?? new SupplierStats(supplierId, 0, 0);
    }
}
