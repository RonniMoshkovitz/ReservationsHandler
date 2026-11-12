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
    public void CreateLimiter()
    {
        _time = new FakeTimeProvider();
        _limiter = new SlidingWindowRateLimiter(Options.Create(new RateLimitOptions()), _time);   // 100 per 60 seconds
    }

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
    public void RejectedRequests_DoNotExtendTheWindow()
    {
        Request(_limiter, "s1", 100);
        _time.Advance(TimeSpan.FromSeconds(30));
        Assert.That(Request(_limiter, "s1", 10), Is.EqualTo(0), "all rejected");

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
}
