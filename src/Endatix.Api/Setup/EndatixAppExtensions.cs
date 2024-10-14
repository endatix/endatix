using Endatix.Framework.Hosting;
using Endatix.Api.Setup;
using FastEndpoints;
using FastEndpoints.Swagger;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using Endatix.Api.Infrastructure;

namespace Endatix.Setup;

/// <summary>
/// Provides extension methods for configuring and setting up the Endatix application
/// </summary>
public static class EndatixAppExtensions
{
    /// <summary>
    /// Adds the API Endpoints associated provided by the Endatix app
    /// </summary>
    /// <param name="endatixApp"></param>
    /// <returns>An instance of <see cref="IEndatixApp"/> representing the configured application.</returns>
    public static IEndatixApp AddApiEndpoints(this IEndatixApp endatixApp)
    {
        endatixApp.Services.AddCorsMiddleware();
        endatixApp.Services.AddDefaultJsonOptions();

        endatixApp.Services
                .AddFastEndpoints()
                .SwaggerDocument(o =>
                    {
                        o.ShortSchemaNames = true;
                        o.DocumentSettings = s =>
                        {
                            s.Version = "v0.1.0";
                            s.DocumentName = "Alpha Release";
                            s.Title = "Endatix API";
                        };
                    });

        return endatixApp;
    }

    /// <summary>
    /// Adds the default JSON options Endatix needs for minimal API results.
    /// </summary>
    private static IServiceCollection AddDefaultJsonOptions(this IServiceCollection services)
    {
        services.Configure<JsonOptions>(options => options.SerializerOptions.Converters.Add(new LongToStringConverter()));

        return services;
    }
}
