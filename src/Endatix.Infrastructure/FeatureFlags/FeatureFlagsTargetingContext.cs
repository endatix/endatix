using Endatix.Core.Abstractions;
using Microsoft.FeatureManagement.FeatureFilters;

namespace Endatix.Infrastructure.FeatureFlags;

/// <summary>
/// Microsoft Feature Management targeting using tenant and user context from Core abstractions.
/// </summary>
internal sealed class FeatureFlagsTargetingContext(ITenantContext tenantContext, IUserContext userContext)
    : ITargetingContextAccessor
{
    private const string AnonymousUserId = "anonymous";

    /// <inheritdoc />
    public ValueTask<TargetingContext> GetContextAsync()
    {
        var context = new TargetingContext
        {
            Groups = GetUserGroups(tenantContext),
            UserId = GetUserId(userContext),
        };

        return new ValueTask<TargetingContext>(context);
    }

    private static string[] GetUserGroups(ITenantContext tenantContext)
    {
        var tenant = tenantContext.TenantId;
        if (tenant <= 0)
        {
            return Array.Empty<string>();
        }

        return [$"tenant-{tenant}"];
    }

    private static string GetUserId(IUserContext userContext) =>
        userContext.GetCurrentUserId() ?? AnonymousUserId;
}
