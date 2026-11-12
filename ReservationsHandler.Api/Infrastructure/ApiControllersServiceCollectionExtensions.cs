using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc.Formatters;

namespace ReservationsHandler.Api.Infrastructure;

public static class ApiControllersServiceCollectionExtensions
{
    /// <summary>Registers the controllers, configured as a JSON-only API whose enums travel as names.</summary>
    public static IMvcBuilder AddApiControllers(this IServiceCollection services) =>
        services
            .AddControllers(options =>
            {
                // The API speaks JSON only. By default Swagger also offers text/plain and text/json responses that no
                // client wants. Removing them here (instead of forcing a content type with [Produces], which would
                // also overwrite the application/problem+json type of the error responses) keeps the documentation
                // and the real behavior in step.
                options.OutputFormatters.RemoveType<StringOutputFormatter>();
                options.OutputFormatters.OfType<SystemTextJsonOutputFormatter>().Single().SupportedMediaTypes.Remove("text/json");
            })
            // Enums travel as names ("Created"), not numbers. Swagger reads these same options, so the documented
            // schema matches what is really sent.
            .AddJsonOptions(options => options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
}
