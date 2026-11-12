using Microsoft.Extensions.Logging.Abstractions;
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
    private FakeRateLimiter _limiter = null!;
    private FakeReservationRepository _reservations = null!;
    private FakeSupplierStatsRepository _stats = null!;
    private ReservationIngestionService _service = null!;

    [SetUp]
    public void CreateService()
    {
        _limiter = new FakeRateLimiter();
        _reservations = new FakeReservationRepository();
        _stats = new FakeSupplierStatsRepository();
        _service = new ReservationIngestionService(_limiter, _reservations, _stats, NullLogger<ReservationIngestionService>.Instance);
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

    // The limit is per supplier: different reservations from one supplier share a quota, other suppliers have their own.
    [Test]
    public async Task WithTheRealLimiter_TheQuotaIsPerSupplier_NotPerReservation()
    {
        var limiter = new SlidingWindowRateLimiter(
            Options.Create(new RateLimitOptions { PermitLimit = 1, Window = TimeSpan.FromSeconds(10) }),
            new FakeTimeProvider());
        var service = new ReservationIngestionService(limiter, _reservations, _stats, NullLogger<ReservationIngestionService>.Instance);

        var firstFromA = await service.IngestAsync(Create("supplier-a", "res-1"));
        var secondFromA = await service.IngestAsync(Create("supplier-a", "res-2"));
        var firstFromB = await service.IngestAsync(Create("supplier-b", "res-1"));

        Assert.Multiple(() =>
        {
            Assert.That(firstFromA.Status, Is.EqualTo(IngestStatus.Created));
            Assert.That(secondFromA.Status, Is.EqualTo(IngestStatus.Throttled), "same supplier, new reservation: same quota");
            Assert.That(firstFromB.Status, Is.EqualTo(IngestStatus.Created), "another supplier has its own quota");
        });
    }

    // The assignment throttles on requests SENT, not on reservations saved, so a resend that the repository
    // reports as Duplicate or Stale must still use up the supplier's quota.
    [TestCase(IngestStatus.Duplicate)]
    [TestCase(IngestStatus.Stale)]
    public async Task WithTheRealLimiter_DuplicateAndStaleRequestsStillConsumeQuota(IngestStatus repositoryStatus)
    {
        var limiter = new SlidingWindowRateLimiter(
            Options.Create(new RateLimitOptions { PermitLimit = 1, Window = TimeSpan.FromSeconds(10) }),
            new FakeTimeProvider());
        var service = new ReservationIngestionService(limiter, _reservations, _stats, NullLogger<ReservationIngestionService>.Instance);
        _reservations.Status = repositoryStatus;

        var first = await service.IngestAsync(Create());
        var second = await service.IngestAsync(Create());

        Assert.Multiple(() =>
        {
            Assert.That(first, Is.EqualTo(IngestResult.Processed(repositoryStatus)));
            Assert.That(second.Status, Is.EqualTo(IngestStatus.Throttled));
            Assert.That(_reservations.Upserted, Has.Count.EqualTo(1), "the second request never reached the repository");
        });
    }
}
