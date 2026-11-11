using System.ComponentModel.DataAnnotations;
using ReservationsHandler.Api.Contracts;

namespace ReservationsHandler.Api.Tests.Contracts;

[TestFixture]
public class IngestReservationRequestTests
{
    private static readonly DateTimeOffset CheckIn = new(2026, 8, 1, 0, 0, 0, TimeSpan.Zero);

    /// <summary>Mutable stand-in for the request, so tests can tweak one field with <c>with</c>.</summary>
    public sealed record Values
    {
        public string? SupplierId { get; init; } = "supplier-1";
        public string? ReservationId { get; init; } = "res-1";
        public string? RoomId { get; init; } = "room-1";
        public DateTimeOffset? CheckIn { get; init; } = IngestReservationRequestTests.CheckIn;
        public DateTimeOffset? CheckOut { get; init; } = IngestReservationRequestTests.CheckIn.AddDays(2);
        public decimal? Price { get; init; } = 450.00m;
        public DateTimeOffset? UpdatedAtUtc { get; init; } = new DateTimeOffset(2026, 7, 1, 10, 15, 0, TimeSpan.Zero);

        public IngestReservationRequest ToRequest() => new()
        {
            SupplierId = SupplierId,
            ReservationId = ReservationId,
            RoomId = RoomId,
            CheckIn = CheckIn,
            CheckOut = CheckOut,
            Price = Price,
            UpdatedAtUtc = UpdatedAtUtc
        };
    }

    private static List<ValidationResult> Validate(Values values)
    {
        var request = values.ToRequest();
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(request, new ValidationContext(request), results, validateAllProperties: true);
        return results;
    }

    private static IEnumerable<string> FailingMembers(List<ValidationResult> results) =>
        results.SelectMany(r => r.MemberNames);

    [Test]
    public void ValidRequest_PassesValidation()
    {
        Assert.That(Validate(new Values()), Is.Empty);
    }

    private static IEnumerable<TestCaseData> MissingFields()
    {
        yield return Missing(nameof(Values.SupplierId), v => v with { SupplierId = null });
        yield return Missing(nameof(Values.ReservationId), v => v with { ReservationId = null });
        yield return Missing(nameof(Values.RoomId), v => v with { RoomId = null });
        yield return Missing(nameof(Values.CheckIn), v => v with { CheckIn = null });
        yield return Missing(nameof(Values.CheckOut), v => v with { CheckOut = null });
        yield return Missing(nameof(Values.Price), v => v with { Price = null });
        yield return Missing(nameof(Values.UpdatedAtUtc), v => v with { UpdatedAtUtc = null });
    }

    private static TestCaseData Missing(string field, Func<Values, Values> mutate) =>
        new TestCaseData(field, mutate).SetName($"MissingField_{field}_FailsValidation");

    [TestCaseSource(nameof(MissingFields))]
    public void MissingField_FailsValidation(string field, Func<Values, Values> mutate)
    {
        var results = Validate(mutate(new Values()));

        Assert.That(FailingMembers(results), Does.Contain(field));
    }

    [Test]
    public void BlankString_FailsValidation(
        [Values("SupplierId", "ReservationId", "RoomId")] string field,
        [Values("", "   ")] string blank)
    {
        var values = field switch
        {
            "SupplierId" => new Values { SupplierId = blank },
            "ReservationId" => new Values { ReservationId = blank },
            _ => new Values { RoomId = blank }
        };

        Assert.That(FailingMembers(Validate(values)), Does.Contain(field));
    }

    [Test]
    public void CheckOutEqualToCheckIn_FailsValidation()
    {
        var results = Validate(new Values { CheckOut = CheckIn });

        Assert.That(FailingMembers(results), Does.Contain(nameof(Values.CheckOut)));
    }

    [Test]
    public void CheckOutBeforeCheckIn_FailsValidation()
    {
        var results = Validate(new Values { CheckOut = CheckIn.AddDays(-1) });

        Assert.That(FailingMembers(results), Does.Contain(nameof(Values.CheckOut)));
    }

    [Test]
    public void CheckOutOneTickAfterCheckIn_PassesValidation()
    {
        var results = Validate(new Values { CheckOut = CheckIn.AddTicks(1) });

        Assert.That(results, Is.Empty);
    }

    [Test]
    public void CheckOutComparison_UsesInstants_NotOffsets()
    {
        // 02:00+02:00 is the same instant as 00:00Z, so check-out is NOT after check-in.
        var checkOut = new DateTimeOffset(2026, 8, 1, 2, 0, 0, TimeSpan.FromHours(2));

        var results = Validate(new Values { CheckOut = checkOut });

        Assert.That(FailingMembers(results), Does.Contain(nameof(Values.CheckOut)));
    }

    [Test]
    public void CheckOutThatLooksLaterOnTheClockButIsEarlierInTime_FailsValidation()
    {
        var checkIn = new DateTimeOffset(2026, 8, 1, 1, 0, 0, TimeSpan.Zero);
        var checkOut = new DateTimeOffset(2026, 8, 1, 2, 0, 0, TimeSpan.FromHours(3));

        var results = Validate(new Values { CheckIn = checkIn, CheckOut = checkOut });

        Assert.That(FailingMembers(results), Does.Contain(nameof(Values.CheckOut)));
    }

    [Test]
    public void MissingCheckOut_ReportsOnlyTheMissingField_WithoutThrowing()
    {
        List<ValidationResult> results = null!;

        Assert.DoesNotThrow(() => results = Validate(new Values { CheckOut = null }));
        Assert.That(FailingMembers(results), Is.EquivalentTo(new[] { nameof(Values.CheckOut) }));
    }

    [Test]
    public void NegativePrice_FailsValidation()
    {
        var results = Validate(new Values { Price = -0.01m });

        Assert.That(FailingMembers(results), Does.Contain(nameof(Values.Price)));
    }

    [Test]
    public void ZeroPrice_PassesValidation()
    {
        Assert.That(Validate(new Values { Price = 0m }), Is.Empty);
    }

    [Test]
    public void ToReservation_TrimsIdentifiers()
    {
        var reservation = new Values
        {
            SupplierId = "  supplier-1 ",
            ReservationId = " res-1  ",
            RoomId = "  room-1 "
        }.ToRequest().ToReservation();

        Assert.Multiple(() =>
        {
            Assert.That(reservation.SupplierId, Is.EqualTo("supplier-1"));
            Assert.That(reservation.ReservationId, Is.EqualTo("res-1"));
            Assert.That(reservation.RoomId, Is.EqualTo("room-1"));
        });
    }

    [Test]
    public void ToReservation_NormalizesTimestampsToUtc()
    {
        var plusTwo = TimeSpan.FromHours(2);
        var reservation = new Values
        {
            CheckIn = new DateTimeOffset(2026, 8, 1, 2, 0, 0, plusTwo),
            CheckOut = new DateTimeOffset(2026, 8, 3, 2, 0, 0, plusTwo),
            UpdatedAtUtc = new DateTimeOffset(2026, 7, 1, 12, 15, 0, plusTwo)
        }.ToRequest().ToReservation();

        Assert.Multiple(() =>
        {
            Assert.That(reservation.CheckIn.Offset, Is.EqualTo(TimeSpan.Zero));
            Assert.That(reservation.CheckIn, Is.EqualTo(new DateTimeOffset(2026, 8, 1, 0, 0, 0, TimeSpan.Zero)));
            Assert.That(reservation.CheckOut.Offset, Is.EqualTo(TimeSpan.Zero));
            Assert.That(reservation.UpdatedAtUtc.Offset, Is.EqualTo(TimeSpan.Zero));
            Assert.That(reservation.UpdatedAtUtc, Is.EqualTo(new DateTimeOffset(2026, 7, 1, 10, 15, 0, TimeSpan.Zero)));
        });
    }

    [Test]
    public void ToReservation_CopiesPrice()
    {
        var reservation = new Values { Price = 450.10m }.ToRequest().ToReservation();

        Assert.That(reservation.Price, Is.EqualTo(450.10m));
    }
}
