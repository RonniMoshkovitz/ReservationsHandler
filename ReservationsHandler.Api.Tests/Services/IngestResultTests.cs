using ReservationsHandler.Api.Domain;
using ReservationsHandler.Api.Services;

namespace ReservationsHandler.Api.Tests.Services;

[TestFixture]
public class IngestResultTests
{
    [TestCase(IngestStatus.Created)]
    [TestCase(IngestStatus.Updated)]
    [TestCase(IngestStatus.Duplicate)]
    [TestCase(IngestStatus.Stale)]
    public void Processed_CarriesTheStatus_AndNoRetryAfter(IngestStatus status)
    {
        var result = IngestResult.Processed(status);

        Assert.Multiple(() =>
        {
            Assert.That(result.Status, Is.EqualTo(status));
            Assert.That(result.RetryAfter, Is.Null);
        });
    }

    [Test]
    public void Processed_RejectsThrottled_BecauseThatOutcomeNeedsARetryAfter()
    {
        Assert.Throws<ArgumentException>(() => IngestResult.Processed(IngestStatus.Throttled));
    }

    [Test]
    public void Throttled_CarriesTheRetryAfter()
    {
        var result = IngestResult.Throttled(TimeSpan.FromSeconds(42));

        Assert.Multiple(() =>
        {
            Assert.That(result.Status, Is.EqualTo(IngestStatus.Throttled));
            Assert.That(result.RetryAfter, Is.EqualTo(TimeSpan.FromSeconds(42)));
        });
    }

    [Test]
    public void Results_AreComparedByValue()
    {
        var created = IngestResult.Processed(IngestStatus.Created);
        var anotherCreated = IngestResult.Processed(IngestStatus.Created);
        var updated = IngestResult.Processed(IngestStatus.Updated);
        var throttledForOneSecond = IngestResult.Throttled(TimeSpan.FromSeconds(1));
        var anotherThrottledForOneSecond = IngestResult.Throttled(TimeSpan.FromSeconds(1));
        var throttledForTwoSeconds = IngestResult.Throttled(TimeSpan.FromSeconds(2));

        Assert.Multiple(() =>
        {
            Assert.That(created, Is.EqualTo(anotherCreated));
            Assert.That(created, Is.Not.EqualTo(updated));
            Assert.That(throttledForOneSecond, Is.EqualTo(anotherThrottledForOneSecond));
            Assert.That(throttledForOneSecond, Is.Not.EqualTo(throttledForTwoSeconds));
        });
    }
}
