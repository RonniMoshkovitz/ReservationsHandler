namespace ReservationsHandler.Api.Data;

public static class DataServiceCollectionExtensions
{
    public const string ConnectionStringName = "Reservations";

    public static IServiceCollection AddDataAccess(this IServiceCollection services)
    {
        DapperConfiguration.RegisterTypeHandlers();

        // Resolved lazily from the final IConfiguration so that test hosts can override the connection string.
        services.AddSingleton<IDbConnectionFactory>(sp =>
        {
            var connectionString = sp.GetRequiredService<IConfiguration>().GetConnectionString(ConnectionStringName)
                ?? throw new InvalidOperationException($"Connection string '{ConnectionStringName}' is not configured.");
            return new SqliteConnectionFactory(connectionString);
        });

        // Repositories are stateless (a connection per call), so singletons are safe.
        services.AddSingleton<DatabaseInitializer>();
        services.AddSingleton<IReservationRepository, ReservationRepository>();
        services.AddSingleton<ISupplierStatsRepository, SupplierStatsRepository>();
        return services;
    }

    public static async Task InitializeDatabaseAsync(this IHost host, CancellationToken cancellationToken = default)
    {
        await host.Services.GetRequiredService<DatabaseInitializer>().InitializeAsync(cancellationToken);
    }
}
