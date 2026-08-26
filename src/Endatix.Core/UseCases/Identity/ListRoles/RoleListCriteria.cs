using Endatix.Core.Infrastructure.Paging;

namespace Endatix.Core.UseCases.Identity.ListRoles;

/// <summary>
/// Domain filters and sort for role list queries. Paging and free-text search
/// travel separately in <see cref="SearchablePageRequest"/>.
/// </summary>
public sealed record RoleListCriteria
{
    /// <summary>
    /// Creates role list criteria, normalizing the role type filter.
    /// </summary>
    /// <param name="roleType">Role type filter (all/system/custom).</param>
    /// <param name="sort">Sort field and direction. When null, defaults to Name ascending.</param>
    public RoleListCriteria(string? roleType = null, SortRequest<RoleListSortBy>? sort = null)
    {
        RoleType = string.IsNullOrWhiteSpace(roleType) ? null : roleType.Trim().ToLowerInvariant();
        Sort = sort;
    }

    /// <summary>
    /// Role type filter, trimmed and lowercased; null when not supplied.
    /// </summary>
    public string? RoleType { get; }

    /// <summary>
    /// Sort field and direction. When null, the read model keeps its default order.
    /// </summary>
    public SortRequest<RoleListSortBy>? Sort { get; }
}
