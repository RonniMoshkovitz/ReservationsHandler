using ReservationsHandler.Api.Domain;

namespace ReservationsHandler.Api.Contracts;

/// <summary>Body returned for every successfully processed ingest request.</summary>
/// <param name="Status">What happened to the reservation: Created, Updated, Duplicate or Stale. A throttled request gets a 429 response instead, never this body.</param>
/// <param name="SupplierId">The supplier id, trimmed as it was stored.</param>
/// <param name="ReservationId">The reservation id, trimmed as it was stored.</param>
public sealed record IngestReservationResponse(IngestStatus Status, string SupplierId, string ReservationId);
