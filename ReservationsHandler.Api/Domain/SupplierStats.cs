namespace ReservationsHandler.Api.Domain;

/// <summary>Per-supplier counters. <paramref name="Ingested"/> counts created plus updated reservations.</summary>
public sealed record SupplierStats(string SupplierId, long Ingested, long Throttled);
