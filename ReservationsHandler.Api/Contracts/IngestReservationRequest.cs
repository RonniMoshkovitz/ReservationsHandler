using System.ComponentModel.DataAnnotations;
using ReservationsHandler.Api.Domain;

namespace ReservationsHandler.Api.Contracts;

/// <summary>
/// Body of <c>POST /api/reservations/ingest</c>.
/// </summary>
public sealed class IngestReservationRequest : IValidatableObject
{
    /// <summary>The sending supplier. Together with <c>reservationId</c> it identifies the reservation. Leading and trailing spaces are ignored.</summary>
    [Required(AllowEmptyStrings = false)]
    public string? SupplierId { get; init; }

    /// <summary>The supplier's own id for the reservation; only unique per supplier. Leading and trailing spaces are ignored.</summary>
    [Required(AllowEmptyStrings = false)]
    public string? ReservationId { get; init; }

    /// <summary>The room the reservation is for.</summary>
    [Required(AllowEmptyStrings = false)]
    public string? RoomId { get; init; }

    /// <summary>Check-in date and time, ISO 8601 with a UTC offset, for example <c>2026-08-01T00:00:00Z</c>.</summary>
    [Required]
    public DateTimeOffset? CheckIn { get; init; }

    /// <summary>Check-out date and time, ISO 8601 with a UTC offset. Must be after <c>checkIn</c>.</summary>
    [Required]
    public DateTimeOffset? CheckOut { get; init; }

    /// <summary>The price; zero or greater.</summary>
    [Required]
    [Range(0, double.MaxValue, ErrorMessage = "Price must be zero or greater.")]
    public decimal? Price { get; init; }

    /// <summary>
    /// When the supplier last changed this reservation, ISO 8601 with a UTC offset. An update older than the
    /// stored one is ignored (reported as Stale), so a delayed retry cannot overwrite newer data.
    /// </summary>
    [Required]
    public DateTimeOffset? UpdatedAtUtc { get; init; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (CheckIn.HasValue && CheckOut.HasValue && CheckOut.Value <= CheckIn.Value)
        {
            yield return new ValidationResult(
                "CheckOut must be after CheckIn.",
                [nameof(CheckOut)]);
        }
    }

    /// <summary>
    /// Maps a request that has already passed validation to the domain model,
    /// trimming identifiers and normalizing every timestamp to UTC.
    /// </summary>
    public Reservation ToReservation() => new(
        SupplierId!.Trim(),
        ReservationId!.Trim(),
        RoomId!.Trim(),
        CheckIn!.Value.ToUniversalTime(),
        CheckOut!.Value.ToUniversalTime(),
        Price!.Value,
        UpdatedAtUtc!.Value.ToUniversalTime());
}
