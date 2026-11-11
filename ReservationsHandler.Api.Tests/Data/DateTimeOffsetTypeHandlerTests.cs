using System.Data;
using Microsoft.Data.Sqlite;
using ReservationsHandler.Api.Data;

namespace ReservationsHandler.Api.Tests.Data;

[TestFixture]
public class DateTimeOffsetTypeHandlerTests
{
    private static readonly DateTimeOffset Midnight = new(2026, 8, 1, 0, 0, 0, TimeSpan.Zero);
    private readonly DateTimeOffsetTypeHandler _handler = new();

    private string ToText(DateTimeOffset value)
    {
        var parameter = new SqliteParameter();
        _handler.SetValue(parameter, value);
        return (string)parameter.Value!;
    }

    [Test]
    public void SetValue_WritesFixedWidthIsoUtcTextAsAString()
    {
        var parameter = new SqliteParameter();

        _handler.SetValue(parameter, Midnight);

        Assert.Multiple(() =>
        {
            Assert.That(parameter.DbType, Is.EqualTo(DbType.String));
            Assert.That(parameter.Value, Is.EqualTo("2026-08-01T00:00:00.0000000Z"));
        });
    }

    [Test]
    public void SetValue_NormalizesOtherOffsetsToUtc()
    {
        var plusTwo = new DateTimeOffset(2026, 8, 1, 2, 0, 0, TimeSpan.FromHours(2));

        Assert.That(ToText(plusTwo), Is.EqualTo("2026-08-01T00:00:00.0000000Z"));
    }

    [Test]
    public void SetValue_KeepsTickPrecision()
    {
        Assert.That(ToText(Midnight.AddTicks(1)), Is.EqualTo("2026-08-01T00:00:00.0000001Z"));
    }

    [Test]
    [SetCulture("th-TH")] // Buddhist calendar: would produce year 2569 if the culture leaked into formatting.
    public void SetValue_IsCultureInvariant()
    {
        Assert.That(ToText(Midnight), Is.EqualTo("2026-08-01T00:00:00.0000000Z"));
    }

    [Test]
    public void Parse_RoundTripsAsTheSameInstantInUtc()
    {
        var original = new DateTimeOffset(2026, 7, 1, 12, 15, 30, 123, TimeSpan.FromHours(2)).AddTicks(4567);

        var parsed = _handler.Parse(ToText(original));

        Assert.Multiple(() =>
        {
            Assert.That(parsed, Is.EqualTo(original));
            Assert.That(parsed.Offset, Is.EqualTo(TimeSpan.Zero));
        });
    }

    [Test]
    public void Text_SortsChronologically_AcrossOffsets()
    {
        var chronological = new[]
        {
            new DateTimeOffset(2026, 7, 31, 23, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 8, 1, 1, 0, 0, TimeSpan.FromHours(3)).AddTicks(1), // 2026-07-31T22:00:00.0000001Z
            Midnight,
            Midnight.AddTicks(1),
            new DateTimeOffset(2026, 8, 1, 0, 0, 1, TimeSpan.Zero),
            new DateTimeOffset(2026, 12, 31, 23, 59, 59, TimeSpan.Zero)
        }.OrderBy(d => d).ToArray();

        var sortedAsText = chronological
            .Select(ToText)
            .OrderBy(t => t, StringComparer.Ordinal)
            .Select(t => _handler.Parse(t))
            .ToArray();

        Assert.That(sortedAsText, Is.EqualTo(chronological));
    }

    [TestCase("")]
    [TestCase("garbage")]
    [TestCase("2026-08-01 00:00:00+00:00")] // SQLite's native format, not ours
    [TestCase("2026-08-01T00:00:00Z")]      // missing fractional digits
    public void Parse_Throws_WhenTextIsNotInTheStoredFormat(string text)
    {
        Assert.Throws<FormatException>(() => _handler.Parse(text));
    }

    [Test]
    public void Parse_Throws_WhenTheDatabaseValueIsNotText()
    {
        Assert.Throws<InvalidCastException>(() => _handler.Parse(12345L));
    }
}
