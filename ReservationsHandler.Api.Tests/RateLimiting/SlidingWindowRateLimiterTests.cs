using System.Collections.Concurrent;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using ReservationsHandler.Api.RateLimiting;

namespace ReservationsHandler.Api.Tests.RateLimiting;

[TestFixture]
public class SlidingWindowRateLimiterTests
{
    private FakeTimeProvider _time = null!;
    private SlidingWindowRateLimiter _limiter = null!;

    [SetUp]
    public void CreateDefaultLimiter()
    {
        _time = new FakeTimeProvider();
        _limiter = CreateLimiter(permitLimit: 100, windowSeconds: 60);
    }

    private SlidingWindowRateLimiter CreateLimiter(int permitLimit, int windowSeconds) => new(
        Options.Create(new RateLimitOptions { PermitLimit = permitLimit, Window = TimeSpan.FromSeconds(windowSeconds) }),
        _time);

    /// <summary>Makes <paramref name="count"/> requests and returns how many were permitted.</summary>
    private static int Request(IRateLimiter limiter, string id, int count) =>
        Enumerable.Range(0, count).Count(_ => limiter.TryAcquire(id).Permitted);

    [Test]
    public void First100Requests_ArePermitted_And101stIsRejected()
    {
        Assert.That(Request(_limiter, "s1", 100), Is.EqualTo(100));

        var result = _limiter.TryAcquire("s1");

        Assert.Multiple(() =>
        {
            Assert.That(result.Permitted, Is.False);
            Assert.That(result.RetryAfter, Is.EqualTo(TimeSpan.FromSeconds(60)));
        });
    }

    [Test]
    public void PermittedRequest_HasNoRetryAfter()
    {
        var result = _limiter.TryAcquire("s1");

        Assert.Multiple(() =>
        {
            Assert.That(result.Permitted, Is.True);
            Assert.That(result.RetryAfter, Is.EqualTo(TimeSpan.Zero));
        });
    }

    [Test]
    public void RetryAfter_ShrinksAsTimePasses()
    {
        Request(_limiter, "s1", 100);
        _time.Advance(TimeSpan.FromSeconds(20));

        var result = _limiter.TryAcquire("s1");

        Assert.Multiple(() =>
        {
            Assert.That(result.Permitted, Is.False);
            Assert.That(result.RetryAfter, Is.EqualTo(TimeSpan.FromSeconds(40)));
        });
    }

    [Test]
    public void JustBeforeTheWindowEnds_StillRejected_WithTheRemainingWait()
    {
        Request(_limiter, "s1", 100);
        _time.Advance(TimeSpan.FromSeconds(60) - TimeSpan.FromMilliseconds(1));

        var result = _limiter.TryAcquire("s1");

        Assert.Multiple(() =>
        {
            Assert.That(result.Permitted, Is.False);
            Assert.That(result.RetryAfter, Is.EqualTo(TimeSpan.FromMilliseconds(1)));
        });
    }

    [Test]
    public void ARequestExactlyOneWindowOld_HasExpired()
    {
        Request(_limiter, "s1", 100);
        _time.Advance(TimeSpan.FromSeconds(60));

        Assert.That(_limiter.TryAcquire("s1").Permitted, Is.True);
    }

    [Test]
    public void Window_IsTrulyRolling_RequestsExpireIndependently()
    {
        // 50 requests at t=0 and 50 at t=30 fill the limit.
        Request(_limiter, "s1", 50);
        _time.Advance(TimeSpan.FromSeconds(30));
        Assert.That(Request(_limiter, "s1", 50), Is.EqualTo(50), "the second batch fits in the same window");
        Assert.That(_limiter.TryAcquire("s1").Permitted, Is.False, "the window is full at t=30");

        // At t=60 only the t=0 batch has left the window, so exactly 50 more fit.
        _time.Advance(TimeSpan.FromSeconds(30));
        Assert.That(Request(_limiter, "s1", 60), Is.EqualTo(50));

        // The oldest counted request is now the t=30 batch, which leaves the window in 30s.
        var result = _limiter.TryAcquire("s1");
        Assert.Multiple(() =>
        {
            Assert.That(result.Permitted, Is.False);
            Assert.That(result.RetryAfter, Is.EqualTo(TimeSpan.FromSeconds(30)));
        });
    }

    [Test]
    public void ABurstStraddlingAWindowBoundary_CannotDoubleTheLimit()
    {
        // A fixed 60s window would allow 100 at t=59 and another 100 at t=61. A sliding window must not.
        _time.Advance(TimeSpan.FromSeconds(59));
        Assert.That(Request(_limiter, "s1", 100), Is.EqualTo(100));

        _time.Advance(TimeSpan.FromSeconds(2));

        Assert.That(Request(_limiter, "s1", 100), Is.EqualTo(0));
    }

    [Test]
    public void RejectedRequests_DoNotExtendTheWindow()
    {
        Request(_limiter, "s1", 100);
        _time.Advance(TimeSpan.FromSeconds(30));
        Assert.That(Request(_limiter, "s1", 10), Is.EqualTo(0), "all rejected");

        // If the 10 rejected attempts had counted, the window would still be full here.
        _time.Advance(TimeSpan.FromSeconds(30));

        Assert.That(Request(_limiter, "s1", 100), Is.EqualTo(100));
    }

    [Test]
    public void Ids_AreIsolatedFromEachOther()
    {
        Request(_limiter, "id-a", 100);

        Assert.Multiple(() =>
        {
            Assert.That(_limiter.TryAcquire("id-a").Permitted, Is.False);
            Assert.That(_limiter.TryAcquire("id-b").Permitted, Is.True);
        });
    }

    [Test]
    public void Ids_AreMatchedExactly_IncludingCase()
    {
        Request(_limiter, "id", 100);

        Assert.That(_limiter.TryAcquire("ID").Permitted, Is.True);
    }

    [Test]
    public void CustomOptions_AreHonored()
    {
        var limiter = CreateLimiter(permitLimit: 3, windowSeconds: 10);

        Assert.That(Request(limiter, "s1", 4), Is.EqualTo(3));

        _time.Advance(TimeSpan.FromSeconds(10));
        Assert.That(limiter.TryAcquire("s1").Permitted, Is.True);
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("   ")]
    public void BlankId_Throws(string? id)
    {
        Assert.That(() => _limiter.TryAcquire(id!), Throws.InstanceOf<ArgumentException>());
    }

    [Test]
    public void ConcurrentRequests_NeverExceedTheLimit()
    {
        var results = new ConcurrentBag<RateLimitResult>();

        Parallel.For(0, 300, new ParallelOptions { MaxDegreeOfParallelism = 16 },
            _ => results.Add(_limiter.TryAcquire("s1")));

        var rejected = results.Where(r => !r.Permitted).ToList();
        Assert.Multiple(() =>
        {
            Assert.That(results.Count(r => r.Permitted), Is.EqualTo(100));
            Assert.That(rejected, Has.Count.EqualTo(200));
            Assert.That(rejected.All(r => r.RetryAfter > TimeSpan.Zero), Is.True);
        });
    }

    [Test]
    public void ConcurrentRequests_AcrossManyIds_EachGetExactlyTheLimit()
    {
        var ids = Enumerable.Range(0, 10).Select(i => $"id-{i}").ToArray();
        var permitted = new ConcurrentDictionary<string, int>();

        Parallel.For(0, ids.Length * 150, new ParallelOptions { MaxDegreeOfParallelism = 16 }, i =>
        {
            var id = ids[i % ids.Length];
            if (_limiter.TryAcquire(id).Permitted)
            {
                permitted.AddOrUpdate(id, 1, (_, count) => count + 1);
            }
        });

        Assert.That(ids.Select(id => permitted.GetValueOrDefault(id)), Is.All.EqualTo(100));
    }
}
