using System.Data.Common;

namespace ReservationsHandler.Api.Data;

/// <summary>
/// Hands out open connections to the reservations database, so repositories depend on
/// <see cref="DbConnection"/> and not on a specific provider. Callers dispose the connection.
/// </summary>
public interface IDbConnectionFactory
{
    Task<DbConnection> OpenConnectionAsync(CancellationToken cancellationToken = default);
}
