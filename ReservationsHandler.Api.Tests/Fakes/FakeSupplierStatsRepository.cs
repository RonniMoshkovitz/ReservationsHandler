using ReservationsHandler.Api.Data;
using ReservationsHandler.Api.Domain;

namespace ReservationsHandler.Api.Tests.Fakes;

/// <summary>Records throttled increments and serves configured counters.</summary>
public sealed class FakeSupplierStatsRepository : ISupplierStatsRepository
{
    private readonly List<string> _callLog;

    public FakeSupplierStatsRepository(List<string>? callLog = null)
    {
        _callLog = callLog ?? [];
    }

    public Exception? ExceptionToThrow { get; set; }

    public Dictionary<string, SupplierStats> Stored { get; } = [];

    public List<string> ThrottledSupplierIds { get; } = [];

    public List<CancellationToken> Tokens { get; } = [];

    public Task IncrementThrottledAsync(string supplierId, CancellationToken cancellationToken = default)
    {
        _callLog.Add("stats");
        ThrottledSupplierIds.Add(supplierId);
        Tokens.Add(cancellationToken);

        return ExceptionToThrow is null ? Task.CompletedTask : Task.FromException(ExceptionToThrow);
    }

    public Task<SupplierStats> GetAsync(string supplierId, CancellationToken cancellationToken = default)
    {
        var stats = Stored.GetValueOrDefault(supplierId) ?? new SupplierStats(supplierId, 0, 0);
        return Task.FromResult(stats);
    }
}
