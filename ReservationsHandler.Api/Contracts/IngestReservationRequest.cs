using System.ComponentModel.DataAnnotations;
using ReservationsHandler.Api.Domain;

namespace ReservationsHandler.Api.Contracts;

/// <summary>
/// Body of <c>POST /api/reservations/ingest</c>.
/// </summary>
public sealed class IngestReservationRequest : IValidatableObject
{
    [Required(AllowEmptyStrings = false)]
    public string? SupplierId { get; init; }

    [Required(AllowEmptyStrings = false)]
    public string? ReservationId { get; init; }

    [Required(AllowEmptyStrings = false)]
    public string? RoomId { get; init; }

    [Required]
    public DateTimeOffset? CheckIn { get; init; }

    [Required]
    public DateTimeOffset? CheckOut { get; init; }

    [Required]
    [Range(0, double.MaxValue, ErrorMessage = "Price must be zero or greater.")]
    public decimal? Price { get; init; }

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
