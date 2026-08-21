using Endatix.Framework.FeatureFlags;
using Endatix.Framework.Modules;
using Microsoft.Extensions.Configuration;

namespace Endatix.Api.Infrastructure;

/// <summary>
/// Deployment-level gate for tenant management endpoints.
/// </summary>
/// <remarks>
/// The <see cref="FeatureFlags.MultiTenancy"/> flag is deployment-scoped, so it is read straight from
/// configuration. Disabled deployments answer 404 rather than 403, so a single-tenant install does not
/// advertise the feature's existence.
/// </remarks>
internal static class MultiTenancyGate
{
    internal const string DisabledMessage = "Multi-tenancy is not enabled on this deployment.";

    internal static bool IsEnabled(IConfiguration configuration) =>
        EndatixModuleRegistration.IsFeatureFlagEnabled(configuration, FeatureFlags.MultiTenancy);
}
