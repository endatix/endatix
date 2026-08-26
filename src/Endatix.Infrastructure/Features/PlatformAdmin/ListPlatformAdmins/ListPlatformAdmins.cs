using Endatix.Core.Infrastructure.Paging;
using Endatix.Core.Infrastructure.Result;
using Endatix.Infrastructure.Features.PlatformAdmin.Common;

namespace Endatix.Infrastructure.Features.PlatformAdmin.ListPlatformAdmins;

/// <summary>
/// Platform-scoped read model for listing users with optional approval scope filters.
/// </summary>
public sealed class ListPlatformAdmins(IPlatformAdminUserListing listing)
{
    /// <summary>
    /// Executes the platform-admin user list query.
    /// </summary>
    public async Task<Result<Paged<PlatformAdminUserListItem>>> ExecuteAsync(
        SearchablePageRequest paging,
        ListPlatformAdminsCriteria criteria,
        CancellationToken cancellationToken = default)
    {
        var platformAdminRoleId = await listing.GetPlatformAdminRoleIdAsync(cancellationToken);
        if (criteria.Scope == PlatformAdminListScope.Approved && platformAdminRoleId is null)
        {
            return Result.Success(Paged<PlatformAdminUserListItem>.Empty(paging.Paging.PageSize));
        }

        var (scopeFilter, prioritizeExternalPlatformAdminRole, prioritizeLocalPlatformAdminRole) =
            ResolveScopeFilter(criteria.Scope, platformAdminRoleId);

        var listingCriteria = new PlatformAdminUserListCriteria(
            platformAdminRoleId,
            scopeFilter,
            criteria.TenantId,
            prioritizeExternalPlatformAdminRole,
            prioritizeLocalPlatformAdminRole,
            criteria.Sort,
            criteria.LastLogin);

        return await listing.ListAsync(paging, listingCriteria, cancellationToken);
    }

    internal static (
        PlatformAdminUserScopeFilter ScopeFilter,
        bool PrioritizeExternalPlatformAdminRole,
        bool PrioritizeLocalPlatformAdminRole)
        ResolveScopeFilter(PlatformAdminListScope scope, long? platformAdminRoleId) =>
        scope switch
        {
            PlatformAdminListScope.Approved => (
                PlatformAdminUserScopeFilter.MustHaveLocalPlatformAdminRole,
                false,
                false),
            PlatformAdminListScope.Candidates when platformAdminRoleId is null => (
                PlatformAdminUserScopeFilter.IgnoreLocalPlatformAdminRole,
                true,
                false),
            PlatformAdminListScope.Candidates => (
                PlatformAdminUserScopeFilter.MustNotHaveLocalPlatformAdminRole,
                true,
                false),
            _ => (
                PlatformAdminUserScopeFilter.IgnoreLocalPlatformAdminRole,
                false,
                true),
        };
}
