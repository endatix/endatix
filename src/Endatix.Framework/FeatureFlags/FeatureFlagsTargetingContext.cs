using Endatix.Core.Abstractions;
using Microsoft.FeatureManagement.FeatureFilters;

namespace Endatix.Framework.FeatureFlags;

/// <summary>
/// Provides access to the current feature flags targeting context.
/// </summary>
internal sealed class FeatureFlagsTargetingContext(ITenantContext tenantContext, IUserContext userContext) : ITargetingContextAccessor
{
    private const string ANONYMOUS_USER_ID = "anonymous";
    private static string[] GetUserGroups(ITenantContext tenantContext)
    {
        var tenant = tenantContext.TenantId;
        var userTenantGroup = $"tenant-{tenant}";

        return [userTenantGroup];
    }

    private static string GetUserId(IUserContext userContext) => userContext.GetCurrentUserId() ?? ANONYMOUS_USER_ID;

    /// <inheritdoc />
    public ValueTask<TargetingContext> GetContextAsync()
    {
        var context = new TargetingContext()
        {
            Groups = GetUserGroups(tenantContext),
            UserId = GetUserId(userContext),
        };

        return new ValueTask<TargetingContext>(context);
    }
}
