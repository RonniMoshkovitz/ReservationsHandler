using System.ComponentModel.DataAnnotations;
using ReservationsHandler.Api.Contracts;

namespace ReservationsHandler.Api.Tests.Contracts;

[TestFixture]
public class IngestReservationRequestTests
{
    private static readonly DateTimeOffset CheckIn = new(2026, 8, 1, 0, 0, 0, TimeSpan.Zero);

    /// <summary>Mutable stand-in for the request, so a test can tweak one field with <c>with</c>.</summary>
    private sealed record Values
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

    private static IEnumerable<string> FailingMembers(Values values)
    {
        var request = values.ToRequest();
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(request, new ValidationContext(request), results, validateAllProperties: true);
        return results.SelectMany(r => r.MemberNames);
    }

    [Test]
    public void CheckOutEqualToCheckIn_FailsValidation()
    {
        Assert.That(FailingMembers(new Values { CheckOut = CheckIn }), Does.Contain(nameof(Values.CheckOut)));
    }

    [Test]
    public void CheckOutBeforeCheckIn_FailsValidation()
    {
        Assert.That(FailingMembers(new Values { CheckOut = CheckIn.AddDays(-1) }), Does.Contain(nameof(Values.CheckOut)));
    }

    [Test]
    public void CheckOutThatLooksLaterOnTheClockButIsEarlierInTime_FailsValidation()
    {
        // 02:00+03:00 reads later than 01:00Z, but it is 23:00Z the day before: the comparison is by instant.
        var checkIn = new DateTimeOffset(2026, 8, 1, 1, 0, 0, TimeSpan.Zero);
        var checkOut = new DateTimeOffset(2026, 8, 1, 2, 0, 0, TimeSpan.FromHours(3));

        Assert.That(FailingMembers(new Values { CheckIn = checkIn, CheckOut = checkOut }), Does.Contain(nameof(Values.CheckOut)));
    }

    [Test]
    public void NegativePrice_FailsValidation()
    {
        Assert.That(FailingMembers(new Values { Price = -0.01m }), Does.Contain(nameof(Values.Price)));
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
}
