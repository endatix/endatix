using Ardalis.GuardClauses;
using Endatix.Framework.Hosting;
using Microsoft.Extensions.DependencyInjection;

namespace Endatix.Framework.Setup;

/// <summary>
/// Service collection extensions for the <see cref="Endatix.Framework"/> project
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers the default services implementations shipped with the <see cref="Endatix.Framework"/> project
    /// </summary>
    /// <param name="services">The <see cref="IServiceCollection"/> services collection</param>
    /// <returns>The updated <see cref="IServiceCollection"/> services collection</returns>
    public static IServiceCollection AddEndatixFrameworkServices(this IServiceCollection services)
    {
        Guard.Against.Null(services);

        services.AddSingleton<IAppEnvironment, AppEnvironment>();

        return services;
    }
}
