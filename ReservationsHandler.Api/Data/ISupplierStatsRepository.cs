using ReservationsHandler.Api.Domain;

namespace ReservationsHandler.Api.Data;

public interface ISupplierStatsRepository
{
    /// <summary>Atomically records one throttled request for the supplier.</summary>
    Task IncrementThrottledAsync(string supplierId, CancellationToken cancellationToken = default);

    /// <summary>Returns the supplier's counters, or zeros if the supplier has never been seen.</summary>
    Task<SupplierStats> GetAsync(string supplierId, CancellationToken cancellationToken = default);
}
