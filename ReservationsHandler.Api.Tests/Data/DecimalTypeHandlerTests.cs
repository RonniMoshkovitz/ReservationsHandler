using System.Globalization;
using ReservationsHandler.Api.Data;

namespace ReservationsHandler.Api.Tests.Data;

[TestFixture]
public class DecimalTypeHandlerTests
{
    private readonly DecimalTypeHandler _handler = new();

    // Attributes cannot hold decimal literals, so the expected values are built here.
    private static IEnumerable<TestCaseData> StoredTextWithExpectedValue()
    {
        yield return new TestCaseData("450.10", 450.10m);
        yield return new TestCaseData("450.1", 450.10m);   // Microsoft.Data.Sqlite's own formatting
        yield return new TestCaseData("0.0", 0m);
        yield return new TestCaseData("0.1", 0.1m);
        yield return new TestCaseData("79228162514264337593543950335.0", decimal.MaxValue);
        yield return new TestCaseData("0.0000000000000000000000000001", 0.0000000000000000000000000001m);
    }

    [TestCaseSource(nameof(StoredTextWithExpectedValue))]
    public void Parse_ReadsStoredTextExactly(string text, decimal expected)
    {
        Assert.That(_handler.Parse(text), Is.EqualTo(expected));
    }

    [Test]
    [SetCulture("de-DE")] // decimal comma: "450.10" would misparse (or throw) if the culture leaked in.
    public void Parse_IsCultureInvariant()
    {
        Assert.That(_handler.Parse("450.10"), Is.EqualTo(450.10m));
    }

    [Test]
    public void Parse_KeepsFullPrecision_WithoutFloatingPointDrift()
    {
        // 0.1m + 0.2m is exactly 0.3m for decimals, but 0.30000000000000004 as doubles.
        var sum = _handler.Parse("0.1") + _handler.Parse("0.2");

        Assert.That(sum.ToString(CultureInfo.InvariantCulture), Is.EqualTo("0.3"));
    }

    [TestCase("")]
    [TestCase("abc")]
    public void Parse_Throws_WhenTextIsNotANumber(string text)
    {
        Assert.Throws<FormatException>(() => _handler.Parse(text));
    }

    [Test]
    public void Parse_Throws_WhenTheDatabaseValueIsNotText()
    {
        Assert.Throws<InvalidCastException>(() => _handler.Parse(450L));
    }
}
