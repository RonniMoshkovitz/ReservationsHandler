using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using ReservationsHandler.Api.RateLimiting;

namespace ReservationsHandler.Api.Tests.RateLimiting;

[TestFixture]
public class RateLimitingServiceCollectionExtensionsTests
{
    private static ServiceProvider BuildProvider(
        Dictionary<string, string?>? settings = null,
        TimeProvider? timeProvider = null)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(settings ?? new Dictionary<string, string?>())
            .Build();

        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(configuration);
        if (timeProvider is not null)
        {
            services.AddSingleton(timeProvider);
        }

        services.AddRateLimiting();
        return services.BuildServiceProvider();
    }

    [Test]
    public void WithoutConfiguration_TheDefaultsApply()
    {
        using var provider = BuildProvider();

        var options = provider.GetRequiredService<IOptions<RateLimitOptions>>().Value;

        Assert.Multiple(() =>
        {
            Assert.That(options.PermitLimit, Is.EqualTo(100));
            Assert.That(options.Window, Is.EqualTo(TimeSpan.FromSeconds(60)));
        });
    }

    [Test]
    public void Configuration_OverridesTheDefaults_AndTheLimiterHonorsIt()
    {
        var time = new FakeTimeProvider();
        using var provider = BuildProvider(
            new Dictionary<string, string?>
            {
                ["RateLimiting:PermitLimit"] = "3",
                ["RateLimiting:Window"] = "00:00:10"
            },
            time);

        var limiter = provider.GetRequiredService<IRateLimiter>();

        var permitted = Enumerable.Range(0, 4).Count(_ => limiter.TryAcquire("s1").Permitted);
        Assert.That(permitted, Is.EqualTo(3));

        time.Advance(TimeSpan.FromSeconds(10));
        Assert.That(limiter.TryAcquire("s1").Permitted, Is.True);
    }

    [Test]
    public void InvalidConfiguration_IsRejectedWhenTheOptionsAreResolved()
    {
        using var provider = BuildProvider(new Dictionary<string, string?> { ["RateLimiting:PermitLimit"] = "0" });

        Assert.That(
            () => provider.GetRequiredService<IOptions<RateLimitOptions>>().Value,
            Throws.InstanceOf<OptionsValidationException>());
    }

    [Test]
    public void InvalidWindow_IsRejectedWhenTheOptionsAreResolved()
    {
        using var provider = BuildProvider(new Dictionary<string, string?> { ["RateLimiting:Window"] = "00:00:00" });

        Assert.That(
            () => provider.GetRequiredService<IOptions<RateLimitOptions>>().Value,
            Throws.InstanceOf<OptionsValidationException>());
    }

    [Test]
    public void TheLimiter_IsASingleton_BecauseItsStateLivesInMemory()
    {
        using var provider = BuildProvider();

        Assert.That(
            provider.GetRequiredService<IRateLimiter>(),
            Is.SameAs(provider.GetRequiredService<IRateLimiter>()));
    }

    [Test]
    public void WithoutARegisteredTimeProvider_TheSystemClockIsUsed()
    {
        using var provider = BuildProvider();

        Assert.That(provider.GetRequiredService<TimeProvider>(), Is.SameAs(TimeProvider.System));
    }

    [Test]
    public void ARegisteredTimeProvider_IsNotReplaced()
    {
        var time = new FakeTimeProvider();
        using var provider = BuildProvider(timeProvider: time);

        Assert.That(provider.GetRequiredService<TimeProvider>(), Is.SameAs(time));
    }
}
