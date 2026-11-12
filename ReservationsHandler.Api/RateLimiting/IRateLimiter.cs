namespace ReservationsHandler.Api.RateLimiting;

/// <summary>Limits how many requests each id may make in a rolling window. The id is any caller-chosen key.</summary>
public interface IRateLimiter
{
    /// <summary>
    /// Asks for permission to process one request for <paramref name="id"/>. A permitted request counts against
    /// that id's limit; a rejected one does not, so a rejected id is permitted again as soon as its oldest
    /// counted request leaves the window.
    /// </summary>
    /// <param name="id">The key being limited, matched exactly (case-sensitive).</param>
    RateLimitResult TryAcquire(string id);
}
