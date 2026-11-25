using Endatix.Core.Infrastructure.Result;
using Endatix.Framework.FeatureFlags;
using Endatix.Framework.Modules;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.Configuration;

namespace Endatix.Api.Infrastructure;

/// <summary>
/// Deployment-level gate for tenant management endpoints. Disabled → 404 (not 403).
/// </summary>
internal static class MultiTenancyGate
{
    internal const string DISABLED_MESSAGE = "Multi-tenancy is not enabled on this deployment.";

    internal static bool IsEnabled(IConfiguration configuration) =>
        EndatixModuleRegistration.IsFeatureFlagEnabled(configuration, FeatureFlags.MultiTenancy);

    internal static ProblemHttpResult? Refusal(IConfiguration configuration) =>
        IsEnabled(configuration) ? null : Result.NotFound(DISABLED_MESSAGE).ToProblem();
}
