using ReservationsHandler.Api.Data;
using ReservationsHandler.Api.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddDataAccess();
builder.Services.AddRateLimiting();

var app = builder.Build();

await app.InitializeDatabaseAsync();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.MapControllers();

app.Run();

// Exposed so WebApplicationFactory<Program> can be used from the test project.
public partial class Program;
