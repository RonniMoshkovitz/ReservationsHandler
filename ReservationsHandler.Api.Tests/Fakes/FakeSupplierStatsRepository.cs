using ReservationsHandler.Api.Data;
using ReservationsHandler.Api.Domain;

namespace ReservationsHandler.Api.Tests.Fakes;

/// <summary>Records which suppliers had a throttled request counted.</summary>
public sealed class FakeSupplierStatsRepository : ISupplierStatsRepository
{
    public List<string> ThrottledSupplierIds { get; } = [];

    public Task IncrementThrottledAsync(string supplierId, CancellationToken cancellationToken = default)
    {
        ThrottledSupplierIds.Add(supplierId);
        return Task.CompletedTask;
    }

    public Task<SupplierStats> GetAsync(string supplierId, CancellationToken cancellationToken = default) =>
        Task.FromResult(new SupplierStats(supplierId, 0, 0));
}
