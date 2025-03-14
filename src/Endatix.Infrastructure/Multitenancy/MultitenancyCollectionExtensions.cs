
using Endatix.Core.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace Endatix.Infrastructure.Multitenancy;

/// <summary>
/// Extension methods for configuring multitenancy services in the application.
/// </summary>
public static class MultitenancyCollectionExtensions
{
    /// <summary>
    /// Adds multitenancy configuration to the service collection.
    /// </summary>
    /// <param name="services">The service collection to add multitenancy services to.</param>
    /// <returns>The service collection for chaining additional configuration.</returns>
    public static IServiceCollection AddMultitenancyConfiguration(this IServiceCollection services)
    {
        services.AddScoped<ITenantContext, TenantContext>();
        return services;
    }
}

