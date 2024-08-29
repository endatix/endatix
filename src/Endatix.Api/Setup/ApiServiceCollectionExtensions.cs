using Endatix.Api.Infrastructure.Cors;
using Microsoft.AspNetCore.Cors.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Endatix.Api.Setup;

public static class ApiServiceCollectionExtensions
{
    /// <summary>
    /// Adds the CORS middleware required to run the <see cref="Endatix.Api" /> project
    /// </summary>
    /// <param name="services">the <see cref="IServiceCollection"/> services</param>
    /// <returns>Updated <see cref="IServiceCollection"/> with CORS related middleware and services</returns>
    public static IServiceCollection AddCorsMiddleware(this IServiceCollection services)
    {
        services.AddCors();
        services.AddOptions<CorsSettings>()
               .BindConfiguration("Endatix:Cors")
               .ValidateDataAnnotations();

        services.AddTransient<IConfigureOptions<CorsOptions>, EndpointsCorsConfigurator>();
        services.AddTransient<IWildcardSearcher, CorsWildcardSearcher>();

        return services;
    }
}
