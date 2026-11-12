using ReservationsHandler.Api.Domain;

namespace ReservationsHandler.Api.Tests.Domain;

[TestFixture]
public class ReservationTests
{
    private static readonly DateTimeOffset CheckIn = new(2026, 8, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset CheckOut = new(2026, 8, 3, 0, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset UpdatedAt = new(2026, 7, 1, 10, 15, 0, TimeSpan.Zero);

    private static Reservation Create() =>
        new("supplier-1", "res-1", "room-1", CheckIn, CheckOut, 450.00m, UpdatedAt);

    private static IEnumerable<TestCaseData> SingleFieldChanges()
    {
        yield return Change("RoomId", r => r with { RoomId = "room-2" });
        yield return Change("CheckIn", r => r with { CheckIn = r.CheckIn.AddDays(1) });
        yield return Change("CheckOut", r => r with { CheckOut = r.CheckOut.AddDays(1) });
        yield return Change("Price", r => r with { Price = 450.01m });
        yield return Change("UpdatedAtUtc", r => r with { UpdatedAtUtc = r.UpdatedAtUtc.AddSeconds(1) });
    }

    private static TestCaseData Change(string field, Func<Reservation, Reservation> mutate) =>
        new TestCaseData(mutate).SetName($"SameDetailsAs_IsFalse_WhenOnly{field}Differs");

    [TestCaseSource(nameof(SingleFieldChanges))]
    public void SameDetailsAs_IsFalse_WhenASingleDetailDiffers(Func<Reservation, Reservation> mutate)
    {
        var original = Create();

        Assert.That(original.SameDetailsAs(mutate(original)), Is.False);
    }

    [Test]
    public void SameDetailsAs_ComparesInstants_NotOffsets()
    {
        var original = Create();
        var sameInstantOtherOffset = original with
        {
            CheckIn = new DateTimeOffset(2026, 8, 1, 2, 0, 0, TimeSpan.FromHours(2))
        };

        Assert.That(original.SameDetailsAs(sameInstantOtherOffset), Is.True);
    }

    [Test]
    public void SameDetailsAs_TreatsPricesWithDifferentScaleAsEqual()
    {
        var original = Create() with { Price = 450.00m };
        var other = original with { Price = 450.0m };

        Assert.That(original.SameDetailsAs(other), Is.True);
    }

    [Test]
    public void Evaluate_NothingStored_IsCreated()
    {
        Assert.That(Reservation.Evaluate(null, Create()), Is.EqualTo(IngestStatus.Created));
    }

    [Test]
    public void Evaluate_IdenticalDetails_IsDuplicate()
    {
        Assert.That(Reservation.Evaluate(Create(), Create()), Is.EqualTo(IngestStatus.Duplicate));
    }

    [Test]
    public void Evaluate_DifferentDetailsWithNewerUpdate_IsUpdated()
    {
        var incoming = Create() with { Price = 500m, UpdatedAtUtc = UpdatedAt.AddMinutes(1) };

        Assert.That(Reservation.Evaluate(Create(), incoming), Is.EqualTo(IngestStatus.Updated));
    }

    [Test]
    public void Evaluate_DifferentDetailsWithSameUpdatedAt_IsUpdated_NotStale()
    {
        var incoming = Create() with { Price = 500m };

        Assert.That(Reservation.Evaluate(Create(), incoming), Is.EqualTo(IngestStatus.Updated));
    }

    [Test]
    public void Evaluate_DifferentDetailsWithOlderUpdate_IsStale()
    {
        var incoming = Create() with { Price = 500m, UpdatedAtUtc = UpdatedAt.AddMinutes(-1) };

        Assert.That(Reservation.Evaluate(Create(), incoming), Is.EqualTo(IngestStatus.Stale));
    }
}
