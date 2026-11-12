using ReservationsHandler.Api.Data;
using ReservationsHandler.Api.Infrastructure;
using ReservationsHandler.Api.RateLimiting;
using ReservationsHandler.Api.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddApiControllers();
builder.Services.AddApiDocumentation();
builder.Services.AddDataAccess();
builder.Services.AddRateLimiting();

// A singleton is safe: the service is stateless and only depends on singletons (limiter and repositories).
builder.Services.AddSingleton<IReservationIngestionService, ReservationIngestionService>();

// Unexpected exceptions become a 500 ProblemDetails with no internals leaked (see GlobalExceptionHandler).
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();

var app = builder.Build();

await app.InitializeDatabaseAsync();

// First in the pipeline, so it also catches exceptions thrown by everything after it.
app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.UseApiDocumentation();
}

app.UseHttpsRedirection();
app.MapControllers();

app.Run();

// Exposed so WebApplicationFactory<Program> can be used from the test project.
public partial class Program;
