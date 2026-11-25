using Endatix.Core.Infrastructure.Result;
using Endatix.Core.UseCases.Tenants;
using Endatix.Framework.FeatureFlags;
using Endatix.Framework.Modules;
using MediatR;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.Configuration;

namespace Endatix.Api.Infrastructure;

/// <summary>
/// Deployment-level gate for tenant management endpoints. Disabled → 404 (not 403).
/// </summary>
internal sealed class MultiTenancyGate(IConfiguration configuration, IMediator mediator)
{
    internal const string DISABLED_MESSAGE = "Multi-tenancy is not enabled on this deployment.";

    internal static bool IsEnabled(IConfiguration configuration) =>
        EndatixModuleRegistration.IsFeatureFlagEnabled(configuration, FeatureFlags.MultiTenancy);

    internal async Task<ProblemHttpResult?> RefusalAsync(PlatformTenantAccess access, CancellationToken ct)
    {
        var result = IsEnabled(configuration) ? await mediator.Send(access, ct) : Result.NotFound(DISABLED_MESSAGE);
        return result.IsSuccess ? null : result.ToProblem();
    }
}
