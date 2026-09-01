using Endatix.Framework.FeatureFlags;
using Endatix.Framework.Modules;
using Microsoft.Extensions.Configuration;

namespace Endatix.Api.Infrastructure;

/// <summary>
/// Deployment-level gate for tenant management endpoints. Disabled → 404 (not 403).
/// </summary>
internal static class MultiTenancyGate
{
    internal const string DisabledMessage = "Multi-tenancy is not enabled on this deployment.";

    internal static bool IsEnabled(IConfiguration configuration) =>
        EndatixModuleRegistration.IsFeatureFlagEnabled(configuration, FeatureFlags.MultiTenancy);
}
