using System.Data.Common;
using Dapper;
using ReservationsHandler.Api.Data;
using ReservationsHandler.Api.Domain;

namespace ReservationsHandler.Api.Tests.Data;

/// <summary>
/// Proves the Dapper setup end to end on a single in-memory SQLite connection: the DateTimeOffset
/// RemoveTypeMap workaround, the read-only decimal handler, and mapping rows into the positional record.
/// Uses its own minimal table on purpose; the repositories and schema are covered by the Step 7 integration tests.
/// </summary>
[TestFixture]
public class DapperConfigurationTests
{
    private const string CreateTable = """
        CREATE TABLE Reservations (
            SupplierId    TEXT NOT NULL,
            ReservationId TEXT NOT NULL,
            RoomId        TEXT NOT NULL,
            CheckIn       TEXT NOT NULL,
            CheckOut      TEXT NOT NULL,
            Price         TEXT NOT NULL,
            UpdatedAtUtc  TEXT NOT NULL
        );
        """;

    private const string InsertSql = """
        INSERT INTO Reservations (SupplierId, ReservationId, RoomId, CheckIn, CheckOut, Price, UpdatedAtUtc)
        VALUES (@SupplierId, @ReservationId, @RoomId, @CheckIn, @CheckOut, @Price, @UpdatedAtUtc);
        """;

    // Column order must match the Reservation constructor: Dapper maps the record by constructor.
    private const string SelectSql = """
        SELECT SupplierId, ReservationId, RoomId, CheckIn, CheckOut, Price, UpdatedAtUtc FROM Reservations;
        """;

    private DbConnection _connection = null!;

    private static Reservation Create(decimal price = 450.10m) => new(
        "supplier-1", "res-1", "room-1",
        new DateTimeOffset(2026, 8, 1, 2, 0, 0, TimeSpan.FromHours(2)), // 00:00Z
        new DateTimeOffset(2026, 8, 3, 0, 0, 0, TimeSpan.Zero),
        price,
        new DateTimeOffset(2026, 7, 1, 10, 15, 0, TimeSpan.Zero));

    [OneTimeSetUp]
    public void RegisterHandlers()
    {
        DapperConfiguration.RegisterTypeHandlers();
    }

    [SetUp]
    public async Task OpenDatabase()
    {
        // A private in-memory database lives only as long as this one open connection.
        _connection = await new SqliteConnectionFactory("Data Source=:memory:").OpenConnectionAsync();
        await _connection.ExecuteAsync(CreateTable);
    }

    [TearDown]
    public async Task CloseDatabase()
    {
        await _connection.DisposeAsync();
    }

    [Test]
    public async Task Dates_AreWrittenAsFixedWidthUtcText()
    {
        await _connection.ExecuteAsync(InsertSql, Create());

        var checkIn = await _connection.QuerySingleAsync<string>("SELECT CheckIn FROM Reservations");

        // The +02:00 input is normalized to UTC rather than stored with its original offset.
        Assert.That(checkIn, Is.EqualTo("2026-08-01T00:00:00.0000000Z"));
    }

    [Test]
    public async Task NullableDates_AreAlsoWrittenAsFixedWidthUtcText()
    {
        DateTimeOffset? value = new DateTimeOffset(2026, 8, 1, 2, 0, 0, TimeSpan.FromHours(2));

        var text = await _connection.QuerySingleAsync<string>("SELECT @value", new { value });

        Assert.That(text, Is.EqualTo("2026-08-01T00:00:00.0000000Z"));
    }

    [Test]
    public async Task Price_IsStoredAsText()
    {
        await _connection.ExecuteAsync(InsertSql, Create());

        var storageType = await _connection.QuerySingleAsync<string>("SELECT typeof(Price) FROM Reservations");

        Assert.That(storageType, Is.EqualTo("text"));
    }

    [Test]
    public async Task Reservation_RoundTripsThroughDapperIntoTheRecord()
    {
        var original = Create();
        await _connection.ExecuteAsync(InsertSql, original);

        var loaded = await _connection.QuerySingleAsync<Reservation>(SelectSql);

        Assert.Multiple(() =>
        {
            Assert.That(loaded, Is.EqualTo(original));
            Assert.That(loaded.CheckIn.Offset, Is.EqualTo(TimeSpan.Zero));
            Assert.That(loaded.Price, Is.EqualTo(450.10m));
        });
    }

    [Test]
    public async Task ReadBackReservation_IsADuplicateOfTheOriginal()
    {
        // The property the whole dedup feature depends on: what was stored compares as identical.
        var original = Create();
        await _connection.ExecuteAsync(InsertSql, original);

        var loaded = await _connection.QuerySingleAsync<Reservation>(SelectSql);

        Assert.That(Reservation.Evaluate(loaded, original), Is.EqualTo(IngestStatus.Duplicate));
    }

    // Attributes cannot hold decimal literals, so the prices are built here.
    private static IEnumerable<TestCaseData> Prices()
    {
        yield return new TestCaseData(450.10m);
        yield return new TestCaseData(450.00m);
        yield return new TestCaseData(0.1m);
        yield return new TestCaseData(0m);
        yield return new TestCaseData(decimal.MaxValue);
        yield return new TestCaseData(0.0000000000000000000000000001m);
    }

    [TestCaseSource(nameof(Prices))]
    [SetCulture("de-DE")] // decimal comma: any culture leak into formatting or parsing would break these.
    public async Task Price_RoundTripsExactly_UnderACommaDecimalCulture(decimal price)
    {
        await _connection.ExecuteAsync(InsertSql, Create(price));

        var loaded = await _connection.QuerySingleAsync<Reservation>(SelectSql);

        Assert.That(loaded.Price, Is.EqualTo(price));
    }

    [Test]
    public async Task RegisteringTwice_IsHarmless()
    {
        DapperConfiguration.RegisterTypeHandlers();
        DapperConfiguration.RegisterTypeHandlers();

        await _connection.ExecuteAsync(InsertSql, Create());
        var loaded = await _connection.QuerySingleAsync<Reservation>(SelectSql);

        Assert.That(loaded, Is.EqualTo(Create()));
    }
}
