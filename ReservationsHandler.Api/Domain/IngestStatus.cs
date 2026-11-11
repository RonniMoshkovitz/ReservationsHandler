namespace ReservationsHandler.Api.Domain;

/// <summary>The outcome of an ingest request.</summary>
public enum IngestStatus
{
    /// <summary>No reservation with this identity existed, a new one was saved.</summary>
    Created,

    /// <summary>The reservation existed with different details, it was updated.</summary>
    Updated,

    /// <summary>The reservation already existed with identical details, nothing was written.</summary>
    Duplicate,

    /// <summary>The update is older than the stored one (by updatedAtUtc), nothing was written.</summary>
    Stale,

    /// <summary>The supplier exceeded its rate limit, the request was rejected.</summary>
    Throttled
}
