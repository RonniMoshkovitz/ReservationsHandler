using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using ReservationsHandler.Api.RateLimiting;

namespace ReservationsHandler.Api.Tests.RateLimiting;

/// <summary>
/// ValidateOnStart only takes effect when a real host starts; resolving IOptions.Value would validate lazily
/// even without it. These tests start an actual host to prove a bad limit stops the app at startup.
/// </summary>
[TestFixture]
public class RateLimitingStartupValidationTests
{
    private static IHost BuildHost(Dictionary<string, string?> settings)
    {
        // DisableDefaults keeps appsettings and environment variables from leaking into the test.
        var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings { DisableDefaults = true });
        builder.Configuration.AddInMemoryCollection(settings);
        builder.Services.AddRateLimiting();
        return builder.Build();
    }

    [TestCase("RateLimiting:PermitLimit", "0")]
    [TestCase("RateLimiting:PermitLimit", "-1")]
    [TestCase("RateLimiting:Window", "00:00:00")]
    [TestCase("RateLimiting:Window", "2.00:00:00")]
    public void StartingTheHost_Fails_WhenTheRateLimitConfigurationIsInvalid(string key, string value)
    {
        using var host = BuildHost(new Dictionary<string, string?> { [key] = value });

        Assert.That(
            async () => await host.StartAsync(),
            Throws.InstanceOf<OptionsValidationException>());
    }

    [Test]
    public async Task StartingTheHost_Succeeds_WhenTheConfigurationIsValid()
    {
        using var host = BuildHost(new Dictionary<string, string?>
        {
            ["RateLimiting:PermitLimit"] = "100",
            ["RateLimiting:Window"] = "00:01:00"
        });

        await host.StartAsync();
        await host.StopAsync();

        Assert.Pass();
    }

    [Test]
    public async Task StartingTheHost_Succeeds_WithNoConfigurationAtAll()
    {
        using var host = BuildHost(new Dictionary<string, string?>());

        await host.StartAsync();
        var options = host.Services.GetRequiredService<IOptions<RateLimitOptions>>().Value;
        await host.StopAsync();

        Assert.That(options.PermitLimit, Is.EqualTo(100));
    }
}
