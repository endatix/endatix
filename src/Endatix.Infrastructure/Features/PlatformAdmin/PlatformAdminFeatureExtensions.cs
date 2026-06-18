using Endatix.Infrastructure.Features.PlatformAdmin.Common;
using Endatix.Infrastructure.Features.PlatformAdmin.ListPlatformTenants;
using Microsoft.Extensions.DependencyInjection;
using ListPlatformAdminsQuery = Endatix.Infrastructure.Features.PlatformAdmin.ListPlatformAdmins.ListPlatformAdmins;
using ListPlatformTenantsImpl = Endatix.Infrastructure.Features.PlatformAdmin.ListPlatformTenants.ListPlatformTenants;

namespace Endatix.Infrastructure.Features.PlatformAdmin;

/// <summary>
/// Registers platform-admin read models (vertical slice queries).
/// Mirrors module-style registration such as <c>AddAgentsModule</c> in SaaS.
/// </summary>
public static class PlatformAdminFeatureExtensions
{
    /// <summary>
    /// Adds platform-admin read models (vertical slice queries).
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection.</returns>
    public static IServiceCollection AddPlatformAdminFeatures(this IServiceCollection services)
    {
        services.AddScoped<IPlatformAdminUserListing, PlatformAdminUserListing>();
        services.AddScoped<ListPlatformAdminsQuery>();
        services.AddScoped<IListPlatformTenants, ListPlatformTenantsImpl>();

        return services;
    }
}
