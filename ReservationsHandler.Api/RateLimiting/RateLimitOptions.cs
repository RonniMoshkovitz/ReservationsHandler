using System.ComponentModel.DataAnnotations;

namespace ReservationsHandler.Api.RateLimiting;

/// <summary>Per-id rate limit: at most <see cref="PermitLimit"/> requests in any rolling <see cref="Window"/>.</summary>
public sealed class RateLimitOptions
{
    public const string SectionName = "RateLimiting";

    [Range(1, int.MaxValue)]
    public int PermitLimit { get; init; } = 100;

    // Positive, with a one-day sanity cap.
    [Range(typeof(TimeSpan), "00:00:00.0000001", "1.00:00:00")]
    public TimeSpan Window { get; init; } = TimeSpan.FromSeconds(60);
}
