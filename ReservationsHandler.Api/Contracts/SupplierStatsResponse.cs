namespace ReservationsHandler.Api.Contracts;

/// <summary>Body of <c>GET /api/reservations/stats/{supplierId}</c>.</summary>
/// <param name="SupplierId">The supplier the counts belong to.</param>
/// <param name="Ingested">Reservations created or updated (duplicates and stale updates are not counted).</param>
/// <param name="Throttled">Requests rejected because the supplier exceeded its rate limit.</param>
public sealed record SupplierStatsResponse(string SupplierId, long Ingested, long Throttled);
