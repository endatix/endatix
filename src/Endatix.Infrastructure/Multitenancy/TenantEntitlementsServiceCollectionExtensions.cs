using System.Diagnostics.CodeAnalysis;
using Endatix.Core.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Endatix.Infrastructure.Multitenancy;

/// <summary>
/// Registers the tenant entitlement seam.
/// </summary>
public static class TenantEntitlementsServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="AllowAllTenantEntitlements"/> as <see cref="ITenantEntitlements"/> unless the host
    /// registered its own implementation first. A host that registers one later also wins, as the last
    /// registration is the one resolved.
    /// </summary>
    /// <remarks>
    /// Scoped, so a host implementation may read the tenant's plan from the database.
    /// </remarks>
    /// <param name="services">The service collection to configure.</param>
    /// <returns>The configured service collection.</returns>
    [Experimental("ENDATIX0001")]
    public static IServiceCollection AddTenantEntitlements(this IServiceCollection services)
    {
        services.TryAddScoped<ITenantEntitlements, AllowAllTenantEntitlements>();
        return services;
    }
}
