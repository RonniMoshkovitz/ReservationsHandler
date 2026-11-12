using System.ComponentModel.DataAnnotations;
using ReservationsHandler.Api.RateLimiting;

namespace ReservationsHandler.Api.Tests.RateLimiting;

[TestFixture]
public class RateLimitOptionsTests
{
    private static List<ValidationResult> Validate(RateLimitOptions options)
    {
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(options, new ValidationContext(options), results, validateAllProperties: true);
        return results;
    }

    private static IEnumerable<string> FailingMembers(List<ValidationResult> results) =>
        results.SelectMany(r => r.MemberNames);

    [Test]
    public void Defaults_Are100RequestsPer60Seconds_AndAreValid()
    {
        var options = new RateLimitOptions();

        Assert.Multiple(() =>
        {
            Assert.That(options.PermitLimit, Is.EqualTo(100));
            Assert.That(options.Window, Is.EqualTo(TimeSpan.FromSeconds(60)));
            Assert.That(Validate(options), Is.Empty);
        });
    }

    [TestCase(0)]
    [TestCase(-1)]
    public void NonPositivePermitLimit_IsRejected(int permitLimit)
    {
        var results = Validate(new RateLimitOptions { PermitLimit = permitLimit });

        Assert.That(FailingMembers(results), Is.EquivalentTo(new[] { nameof(RateLimitOptions.PermitLimit) }));
    }

    [TestCase(0)]
    [TestCase(-5)]
    public void NonPositiveWindow_IsRejected(int seconds)
    {
        var results = Validate(new RateLimitOptions { Window = TimeSpan.FromSeconds(seconds) });

        Assert.That(FailingMembers(results), Is.EquivalentTo(new[] { nameof(RateLimitOptions.Window) }));
    }

    [Test]
    public void BothInvalid_ReportsBothProblems()
    {
        var results = Validate(new RateLimitOptions { PermitLimit = 0, Window = TimeSpan.Zero });

        Assert.That(
            FailingMembers(results),
            Is.EquivalentTo(new[] { nameof(RateLimitOptions.PermitLimit), nameof(RateLimitOptions.Window) }));
    }

    [Test]
    public void SmallestAndLargestAllowedValues_AreAccepted()
    {
        Assert.Multiple(() =>
        {
            Assert.That(Validate(new RateLimitOptions { PermitLimit = 1, Window = TimeSpan.FromTicks(1) }), Is.Empty);
            Assert.That(Validate(new RateLimitOptions { PermitLimit = int.MaxValue, Window = TimeSpan.FromDays(1) }), Is.Empty);
        });
    }

    // The Range bounds are parsed from strings; validation must not depend on the host's culture.
    [TestCase("de-DE")]
    [TestCase("fr-FR")]
    [TestCase("th-TH")]
    public void Validation_IsCultureIndependent(string cultureName)
    {
        var original = Thread.CurrentThread.CurrentCulture;
        Thread.CurrentThread.CurrentCulture = new System.Globalization.CultureInfo(cultureName);
        try
        {
            Assert.Multiple(() =>
            {
                Assert.That(Validate(new RateLimitOptions()), Is.Empty);
                Assert.That(Validate(new RateLimitOptions { Window = TimeSpan.Zero }), Is.Not.Empty);
            });
        }
        finally
        {
            Thread.CurrentThread.CurrentCulture = original;
        }
    }

    [Test]
    public void WindowLongerThanTheOneDaySanityCap_IsRejected()
    {
        var results = Validate(new RateLimitOptions { Window = TimeSpan.FromDays(1) + TimeSpan.FromTicks(1) });

        Assert.That(FailingMembers(results), Is.EquivalentTo(new[] { nameof(RateLimitOptions.Window) }));
    }
}
