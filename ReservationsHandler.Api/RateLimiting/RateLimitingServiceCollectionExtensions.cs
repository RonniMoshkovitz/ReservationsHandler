using Microsoft.Extensions.DependencyInjection.Extensions;

namespace ReservationsHandler.Api.RateLimiting;

public static class RateLimitingServiceCollectionExtensions
{
    public static IServiceCollection AddRateLimiting(this IServiceCollection services)
    {
        // A bad limit or window stops the app at startup instead of failing (or never throttling) at runtime.
        services.AddOptions<RateLimitOptions>()
            .BindConfiguration(RateLimitOptions.SectionName)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        // Tests replace this with a FakeTimeProvider.
        services.TryAddSingleton(TimeProvider.System);

        // Must be a singleton: the in-memory request logs ARE the limiter's state.
        services.AddSingleton<IRateLimiter, SlidingWindowRateLimiter>();
        return services;
    }
}
