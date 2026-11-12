using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using ReservationsHandler.Api.Domain;
using ReservationsHandler.Api.RateLimiting;
using ReservationsHandler.Api.Services;
using ReservationsHandler.Api.Tests.Fakes;

namespace ReservationsHandler.Api.Tests.Services;

[TestFixture]
public class ReservationIngestionServiceTests
{
    private List<string> _callLog = null!;
    private FakeRateLimiter _limiter = null!;
    private FakeReservationRepository _reservations = null!;
    private FakeSupplierStatsRepository _stats = null!;
    private ReservationIngestionService _service = null!;

    [SetUp]
    public void CreateService()
    {
        _callLog = [];
        _limiter = new FakeRateLimiter(_callLog);
        _reservations = new FakeReservationRepository(_callLog);
        _stats = new FakeSupplierStatsRepository(_callLog);
        _service = new ReservationIngestionService(_limiter, _reservations, _stats);
    }

    private static Reservation Create(string supplierId = "supplier-1", string reservationId = "res-1") => new(
        supplierId, reservationId, "room-1",
        new DateTimeOffset(2026, 8, 1, 0, 0, 0, TimeSpan.Zero),
        new DateTimeOffset(2026, 8, 3, 0, 0, 0, TimeSpan.Zero),
        450.00m,
        new DateTimeOffset(2026, 7, 1, 10, 15, 0, TimeSpan.Zero));

    [TestCase(IngestStatus.Created)]
    [TestCase(IngestStatus.Updated)]
    [TestCase(IngestStatus.Duplicate)]
    [TestCase(IngestStatus.Stale)]
    public async Task WhenPermitted_TheRepositoryOutcomeIsReturnedUnchanged(IngestStatus repositoryStatus)
    {
        _reservations.Status = repositoryStatus;
        var reservation = Create();

        var result = await _service.IngestAsync(reservation);

        Assert.Multiple(() =>
        {
            Assert.That(result, Is.EqualTo(IngestResult.Processed(repositoryStatus)));
            Assert.That(_reservations.Upserted, Is.EqualTo(new[] { reservation }));
            Assert.That(_stats.ThrottledSupplierIds, Is.Empty, "a permitted request is never counted as throttled");
        });
    }

    [Test]
    public async Task WhenThrottled_TheResultCarriesTheRetryAfter_AndTheThrottledCounterIsIncrementedOnce()
    {
        _limiter.Result = RateLimitResult.Reject(TimeSpan.FromSeconds(42));

        var result = await _service.IngestAsync(Create("supplier-9"));

        Assert.Multiple(() =>
        {
            Assert.That(result, Is.EqualTo(IngestResult.Throttled(TimeSpan.FromSeconds(42))));
            Assert.That(_stats.ThrottledSupplierIds, Is.EqualTo(new[] { "supplier-9" }));
        });
    }

    [Test]
    public async Task WhenThrottled_TheRepositoryIsNeverCalled()
    {
        _limiter.Result = RateLimitResult.Reject(TimeSpan.FromSeconds(1));

        await _service.IngestAsync(Create());

        Assert.That(_reservations.Upserted, Is.Empty, "a throttled request must not touch the reservations table");
    }

    [Test]
    public async Task TheLimiterIsAskedAboutTheReservationsSupplier()
    {
        await _service.IngestAsync(Create("supplier-7"));

        Assert.That(_limiter.RequestedIds, Is.EqualTo(new[] { "supplier-7" }));
    }

    [Test]
    public async Task TheLimiterIsConsultedBeforeAnyDatabaseWork_WhenPermitted()
    {
        await _service.IngestAsync(Create());

        Assert.That(_callLog, Is.EqualTo(new[] { "limiter", "repository" }));
    }

    [Test]
    public async Task OnlyTheLimiterAndTheStatsAreTouched_WhenThrottled()
    {
        _limiter.Result = RateLimitResult.Reject(TimeSpan.FromSeconds(1));

        await _service.IngestAsync(Create());

        Assert.That(_callLog, Is.EqualTo(new[] { "limiter", "stats" }));
    }

    [Test]
    public async Task TheCancellationToken_IsPassedToTheRepository()
    {
        using var cts = new CancellationTokenSource();

        await _service.IngestAsync(Create(), cts.Token);

        Assert.That(_reservations.Tokens, Is.EqualTo(new[] { cts.Token }));
    }

    [Test]
    public async Task TheCancellationToken_IsPassedToTheStats_WhenThrottled()
    {
        _limiter.Result = RateLimitResult.Reject(TimeSpan.FromSeconds(1));
        using var cts = new CancellationTokenSource();

        await _service.IngestAsync(Create(), cts.Token);

        Assert.That(_stats.Tokens, Is.EqualTo(new[] { cts.Token }));
    }

    [Test]
    public void ARepositoryFailure_Propagates_AndIsNotSwallowed()
    {
        _reservations.ExceptionToThrow = new InvalidOperationException("database is down");

        Assert.That(
            async () => await _service.IngestAsync(Create()),
            Throws.InvalidOperationException.With.Message.EqualTo("database is down"));
    }

    [Test]
    public void AStatsFailure_WhileThrottling_Propagates_AndTheRepositoryIsStillNotCalled()
    {
        _limiter.Result = RateLimitResult.Reject(TimeSpan.FromSeconds(1));
        _stats.ExceptionToThrow = new InvalidOperationException("stats unavailable");

        Assert.Multiple(() =>
        {
            Assert.That(
                async () => await _service.IngestAsync(Create()),
                Throws.InvalidOperationException.With.Message.EqualTo("stats unavailable"));
            Assert.That(_reservations.Upserted, Is.Empty);
        });
    }

    [Test]
    public void ANullReservation_IsRejected_BeforeAnyCollaboratorIsCalled()
    {
        Assert.Multiple(() =>
        {
            Assert.That(async () => await _service.IngestAsync(null!), Throws.InstanceOf<ArgumentNullException>());
            Assert.That(_callLog, Is.Empty);
        });
    }

    [Test]
    public async Task WithTheRealLimiter_OnlyTheRequestsOverTheLimitAreThrottled_AndEachIsCountedOnce()
    {
        // The service composed with the real sliding window limiter (limit 3 per 10s) and a fake clock.
        var time = new FakeTimeProvider();
        var limiter = new SlidingWindowRateLimiter(
            Options.Create(new RateLimitOptions { PermitLimit = 3, Window = TimeSpan.FromSeconds(10) }),
            time);
        var service = new ReservationIngestionService(limiter, _reservations, _stats);

        var results = new List<IngestResult>();
        for (var i = 0; i < 5; i++)
        {
            results.Add(await service.IngestAsync(Create(reservationId: $"res-{i}")));
        }

        Assert.Multiple(() =>
        {
            Assert.That(results.Count(r => r.Status == IngestStatus.Created), Is.EqualTo(3));
            Assert.That(results.Count(r => r.Status == IngestStatus.Throttled), Is.EqualTo(2));
            Assert.That(results.Where(r => r.Status == IngestStatus.Throttled).Select(r => r.RetryAfter),
                Is.All.EqualTo(TimeSpan.FromSeconds(10)));
            Assert.That(_reservations.Upserted, Has.Count.EqualTo(3), "throttled requests never reach the repository");
            Assert.That(_stats.ThrottledSupplierIds, Is.EqualTo(new[] { "supplier-1", "supplier-1" }));
        });
    }

    [Test]
    public async Task WithTheRealLimiter_AThrottledSupplierDoesNotAffectAnother_AndRecoversAfterTheWindow()
    {
        var time = new FakeTimeProvider();
        var limiter = new SlidingWindowRateLimiter(
            Options.Create(new RateLimitOptions { PermitLimit = 1, Window = TimeSpan.FromSeconds(10) }),
            time);
        var service = new ReservationIngestionService(limiter, _reservations, _stats);

        await service.IngestAsync(Create("noisy", "a"));
        var noisyThrottled = await service.IngestAsync(Create("noisy", "b"));
        var otherSupplier = await service.IngestAsync(Create("quiet", "a"));

        time.Advance(TimeSpan.FromSeconds(10));
        var noisyRecovered = await service.IngestAsync(Create("noisy", "c"));

        Assert.Multiple(() =>
        {
            Assert.That(noisyThrottled.Status, Is.EqualTo(IngestStatus.Throttled));
            Assert.That(otherSupplier.Status, Is.Not.EqualTo(IngestStatus.Throttled));
            Assert.That(noisyRecovered.Status, Is.Not.EqualTo(IngestStatus.Throttled));
            Assert.That(_stats.ThrottledSupplierIds, Is.EqualTo(new[] { "noisy" }));
        });
    }
}
