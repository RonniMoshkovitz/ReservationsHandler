using System.Globalization;
using Microsoft.AspNetCore.Mvc;
using ReservationsHandler.Api.Contracts;
using ReservationsHandler.Api.Data;
using ReservationsHandler.Api.Domain;
using ReservationsHandler.Api.Services;

namespace ReservationsHandler.Api.Controllers;

[ApiController]
[Route("api/reservations")]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError, "application/problem+json")]
public sealed class ReservationsController : ControllerBase
{
    private readonly IReservationIngestionService _ingestion;

    // The stats endpoint is a plain read with no logic, so it uses the repository directly instead of a
    // pass-through service. Only ingestion has orchestration (throttle, then write) that deserves a service.
    private readonly ISupplierStatsRepository _stats;

    public ReservationsController(IReservationIngestionService ingestion, ISupplierStatsRepository stats)
    {
        _ingestion = ingestion;
        _stats = stats;
    }

    /// <summary>Saves a supplier's reservation update, ignoring exact duplicates and stale updates.</summary>
    /// <remarks>
    /// A reservation is identified by the supplier id plus the reservation id. Resending identical details changes
    /// nothing (<c>Duplicate</c>); different details update the stored reservation (<c>Updated</c>); an update older
    /// than the stored one, by <c>updatedAtUtc</c>, is ignored (<c>Stale</c>). Every request counts against the
    /// supplier's rate limit, including duplicates.
    /// </remarks>
    /// <response code="201">The reservation did not exist and was created. <c>status</c> is <c>Created</c>.</response>
    /// <response code="200">Nothing new was created. <c>status</c> is <c>Updated</c>, <c>Duplicate</c> (nothing written) or <c>Stale</c> (nothing written).</response>
    /// <response code="400">The request body is invalid. The response lists each problem by field.</response>
    /// <response code="429">The supplier exceeded its rate limit. Wait the number of seconds in the <c>Retry-After</c> response header (whole seconds, rounded up) before retrying.</response>
    /// <response code="500">An unexpected error occurred. The body carries only a trace id; nothing about the failure is exposed.</response>
    [HttpPost("ingest")]
    [Consumes("application/json")]
    [ProducesResponseType<IngestReservationResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<IngestReservationResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status429TooManyRequests, "application/problem+json")]
    public async Task<IActionResult> Ingest(IngestReservationRequest request, CancellationToken cancellationToken)
    {
        var reservation = request.ToReservation();
        var result = await _ingestion.IngestAsync(reservation, cancellationToken);

        if (result.RetryAfter is { } retryAfter)
        {
            return TooManyRequests(retryAfter);
        }

        var body = new IngestReservationResponse(result.Status, reservation.SupplierId, reservation.ReservationId);

        return result.Status == IngestStatus.Created
            ? StatusCode(StatusCodes.Status201Created, body)
            : Ok(body);
    }

    /// <summary>How many reservations were ingested (created or updated) and how many requests were throttled.</summary>
    /// <param name="supplierId">The supplier. Leading and trailing spaces are ignored.</param>
    /// <param name="cancellationToken">Cancelled if the client disconnects.</param>
    /// <response code="200">The counts. A supplier that has never sent anything gets zeros.</response>
    /// <response code="400">The supplier id is blank.</response>
    /// <response code="500">An unexpected error occurred. The body carries only a trace id; nothing about the failure is exposed.</response>
    [HttpGet("stats/{supplierId}")]
    [ProducesResponseType<SupplierStatsResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest, "application/problem+json")]
    public async Task<ActionResult<SupplierStatsResponse>> GetStats(string supplierId, CancellationToken cancellationToken)
    {
        // Ids are trimmed on ingest, so the same id is trimmed here. A blank one is an HTTP-level error; the
        // repository throws on blank ids, so it must not be reached with one.
        if (string.IsNullOrWhiteSpace(supplierId))
        {
            ModelState.AddModelError(nameof(supplierId), "The supplier id must not be blank.");
            return ValidationProblem(ModelState);
        }

        var stats = await _stats.GetAsync(supplierId.Trim(), cancellationToken);
        return new SupplierStatsResponse(stats.SupplierId, stats.Ingested, stats.Throttled);
    }

    private ObjectResult TooManyRequests(TimeSpan retryAfter)
    {
        var seconds = ToRetryAfterSeconds(retryAfter);
        Response.Headers.RetryAfter = seconds.ToString(CultureInfo.InvariantCulture);

        return Problem(
            statusCode: StatusCodes.Status429TooManyRequests,
            title: "Too many requests",
            detail: $"The supplier exceeded its rate limit. Retry after {seconds} second(s).");
    }

    // The HTTP Retry-After header carries whole seconds. Round UP, so a client that waits the stated time is
    // never still inside the window, and never below 1, so the header never tells a client to retry immediately.
    // The limiter returns the exact delay; turning it into a header value is an HTTP concern, so it lives here.
    private static int ToRetryAfterSeconds(TimeSpan retryAfter) =>
        (int)Math.Max(1, Math.Ceiling(retryAfter.TotalSeconds));
}
