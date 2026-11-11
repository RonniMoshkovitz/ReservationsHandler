using System.Text.Json.Serialization;
using ReservationsHandler.Api.Domain;

namespace ReservationsHandler.Api.Contracts;

/// <summary>Body returned for every successfully processed ingest request.</summary>
public sealed record IngestReservationResponse(
    [property: JsonConverter(typeof(JsonStringEnumConverter))] IngestStatus Status,
    string SupplierId,
    string ReservationId);
