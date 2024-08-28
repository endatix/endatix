using Endatix.Api.Infrastructure.Cors;
using Microsoft.AspNetCore.Cors.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Endatix.Api.Setup;

public static class ApiServiceCollectionExtensions
{
    public static IServiceCollection AddApiServices(this IServiceCollection services)
    {
        services.AddOptions<CorsSettings>()
               .BindConfiguration("Endatix:Cors")
               .ValidateDataAnnotations();

        services.AddTransient<IConfigureOptions<CorsOptions>, EndpointsCorsConfigurator>();
        services.AddTransient<IWildcardSearcher, CorsWildcardSearcher>();


        return services;
    }
}
