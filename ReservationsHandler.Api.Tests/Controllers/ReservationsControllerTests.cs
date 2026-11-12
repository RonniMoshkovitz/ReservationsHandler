using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.Extensions.Logging.Abstractions;
using ReservationsHandler.Api.Contracts;
using ReservationsHandler.Api.Controllers;
using ReservationsHandler.Api.Domain;
using ReservationsHandler.Api.RateLimiting;
using ReservationsHandler.Api.Services;
using ReservationsHandler.Api.Tests.Fakes;

namespace ReservationsHandler.Api.Tests.Controllers;

/// <summary>
/// The two HTTP rules a client depends on: a throttled request gets a whole-seconds <c>Retry-After</c> that is
/// rounded up (never 0), and a created reservation answers 201 with no <c>Location</c> header. The controller is
/// called directly on top of the real service and the fakes, so no server is started.
/// </summary>
[TestFixture]
public class ReservationsControllerTests
{
    private FakeRateLimiter _limiter = null!;
    private FakeReservationRepository _reservations = null!;
    private ReservationsController _controller = null!;

    [SetUp]
    public void CreateController()
    {
        _limiter = new FakeRateLimiter();
        _reservations = new FakeReservationRepository();
        var service = new ReservationIngestionService(
            _limiter, _reservations, new FakeSupplierStatsRepository(), NullLogger<ReservationIngestionService>.Instance);

        _controller = new ReservationsController(service, new FakeSupplierStatsRepository())
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() },
            ProblemDetailsFactory = new MinimalProblemDetailsFactory()
        };
    }

    private static IngestReservationRequest ValidRequest() => new()
    {
        SupplierId = "supplier-1",
        ReservationId = "res-1",
        RoomId = "room-1",
        CheckIn = new DateTimeOffset(2026, 8, 1, 0, 0, 0, TimeSpan.Zero),
        CheckOut = new DateTimeOffset(2026, 8, 3, 0, 0, 0, TimeSpan.Zero),
        Price = 450.00m,
        UpdatedAtUtc = new DateTimeOffset(2026, 7, 1, 10, 15, 0, TimeSpan.Zero)
    };

    // Ticks, because TimeSpan.FromMilliseconds rounds to whole milliseconds and would hide the last case.
    [TestCase(0L, "1", Description = "the limiter never returns this, but the header must still never say 0")]
    [TestCase(1L, "1", Description = "one tick left: never 0")]
    [TestCase(10_000L, "1", Description = "1 ms left")]
    [TestCase(600_000_000L, "60", Description = "exactly one window")]
    [TestCase(300_000_000L, "30", Description = "an exact number of seconds is not rounded up further")]
    [TestCase(305_000_000L, "31", Description = "30.5 s left rounds up")]
    [TestCase(600_001_000L, "61", Description = "60.0001 s rounds up")]
    public async Task WhenThrottled_Answers429_WithRetryAfterInWholeSecondsRoundedUp(long retryAfterTicks, string expectedHeader)
    {
        _limiter.Result = RateLimitResult.Reject(TimeSpan.FromTicks(retryAfterTicks));

        var result = await _controller.Ingest(ValidRequest(), CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result, Is.TypeOf<ObjectResult>().With.Property("StatusCode").EqualTo(429));
            Assert.That(_controller.Response.Headers.RetryAfter.ToString(), Is.EqualTo(expectedHeader));
            Assert.That(_reservations.Upserted, Is.Empty, "a throttled request never reaches the repository");
        });
    }

    [Test]
    public async Task ACreatedReservation_Answers201_WithNoLocationHeader()
    {
        _reservations.Status = IngestStatus.Created;

        var result = await _controller.Ingest(ValidRequest(), CancellationToken.None);

        // CreatedResult, CreatedAtActionResult and CreatedAtRouteResult all add a Location header when executed,
        // so the exact type being a plain ObjectResult is what guarantees there is none.
        var created = result as ObjectResult;
        Assert.Multiple(() =>
        {
            Assert.That(result, Is.TypeOf<ObjectResult>(), "StatusCode(201, body), never Created/CreatedAtAction: there is no GET-by-id endpoint to point at");
            Assert.That(created?.StatusCode, Is.EqualTo(201));
            Assert.That(created?.Value, Is.EqualTo(new IngestReservationResponse(IngestStatus.Created, "supplier-1", "res-1")));
        });
    }

    /// <summary>The controller's <c>Problem()</c> needs a factory; outside a host there is none, so this supplies one.</summary>
    private sealed class MinimalProblemDetailsFactory : ProblemDetailsFactory
    {
        public override ProblemDetails CreateProblemDetails(
            HttpContext httpContext, int? statusCode = null, string? title = null, string? type = null,
            string? detail = null, string? instance = null) => new()
        {
            Status = statusCode,
            Title = title,
            Type = type,
            Detail = detail,
            Instance = instance
        };

        public override ValidationProblemDetails CreateValidationProblemDetails(
            HttpContext httpContext, ModelStateDictionary modelStateDictionary, int? statusCode = null,
            string? title = null, string? type = null, string? detail = null, string? instance = null) => new(modelStateDictionary)
        {
            Status = statusCode,
            Title = title,
            Type = type,
            Detail = detail,
            Instance = instance
        };
    }
}
