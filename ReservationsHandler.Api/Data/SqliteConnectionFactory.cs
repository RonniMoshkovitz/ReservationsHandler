using System.Data.Common;
using Microsoft.Data.Sqlite;

namespace ReservationsHandler.Api.Data;

/// <summary>A SQLite connection factory.</summary>
public sealed class SqliteConnectionFactory : IDbConnectionFactory
{
    private readonly string _connectionString;

    public SqliteConnectionFactory(string connectionString)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
        _connectionString = connectionString;
    }

    // Microsoft.Data.Sqlite applies "Default Timeout" (30s by default) as the SQLite busy timeout when a
    // connection is opened, so concurrent writers wait instead of failing immediately with "database is locked".
    public async Task<DbConnection> OpenConnectionAsync(CancellationToken cancellationToken = default)
    {
        var connection = new SqliteConnection(_connectionString);
        try
        {
            await connection.OpenAsync(cancellationToken);
            return connection;
        }
        catch
        {
            await connection.DisposeAsync();
            throw;
        }
    }
}
