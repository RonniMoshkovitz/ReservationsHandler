using Microsoft.OpenApi.Models;

namespace ReservationsHandler.Api.Infrastructure;

public static class ApiDocumentationExtensions
{
    /// <summary>
    /// Registers the Swagger/OpenAPI generator. The descriptions come from the XML comments on the controller and
    /// the contracts (see GenerateDocumentationFile in the project file), so those comments are the API docs.
    /// </summary>
    public static IServiceCollection AddApiDocumentation(this IServiceCollection services)
    {
        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen(options =>
        {
            options.SwaggerDoc("v1", new OpenApiInfo
            {
                Title = "Reservations Handler API",
                Version = "v1",
                Description = "Receives reservation updates from suppliers. Exact duplicates and stale updates are ignored, " +
                              "and each supplier is rate limited (100 requests per rolling 60 seconds by default)."
            });

            var xmlFile = $"{typeof(ApiDocumentationExtensions).Assembly.GetName().Name}.xml";
            options.IncludeXmlComments(Path.Combine(AppContext.BaseDirectory, xmlFile));

            // Reference types are non-nullable unless declared with '?', so a never-null response field is not
            // documented as nullable.
            options.SupportNonNullableReferenceTypes();
        });

        return services;
    }

    /// <summary>Serves the OpenAPI document and the Swagger UI.</summary>
    public static IApplicationBuilder UseApiDocumentation(this IApplicationBuilder app)
    {
        app.UseSwagger();
        app.UseSwaggerUI();
        return app;
    }
}
