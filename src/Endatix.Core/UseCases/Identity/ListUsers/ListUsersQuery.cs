using Endatix.Core.Entities.Identity;
using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Paging;
using Endatix.Core.Infrastructure.Result;

namespace Endatix.Core.UseCases.Identity.ListUsers;

/// <summary>
/// Query for listing users in the current tenant. Tenant filter is implicit.
/// </summary>
public sealed record ListUsersQuery : IQuery<Result<Paged<UserWithRoles>>>
{
    public const int DefaultPage = PagedRequestLimits.DEFAULT_PAGE;
    public const int DefaultPageSize = PagedRequestLimits.DEFAULT_PAGE_SIZE;
    public const int MaxPageSize = PagedRequestLimits.MAX_PAGE_SIZE;

    public ListUsersQuery(
        int? page,
        int? pageSize,
        string? search,
        string? role,
        string? status,
        UserListSortBy? sortBy = null,
        bool sortDescending = false,
        DateTime? lastLoginFrom = null,
        DateTime? lastLoginTo = null)
    {
        Page = Math.Max(page ?? DefaultPage, DefaultPage);
        PageSize = Math.Clamp(pageSize ?? DefaultPageSize, 1, MaxPageSize);
        Search = string.IsNullOrWhiteSpace(search) ? null : search.Trim();
        Role = string.IsNullOrWhiteSpace(role) ? null : role.Trim();
        Status = string.IsNullOrWhiteSpace(status) ? null : status.Trim().ToLowerInvariant();
        SortBy = sortBy;
        SortDescending = sortDescending;
        LastLoginFrom = lastLoginFrom;
        LastLoginTo = lastLoginTo;
    }

    public int Page { get; }
    public int PageSize { get; }
    public string? Search { get; }
    public string? Role { get; }
    public string? Status { get; }

    /// <summary>
    /// When null, default order is UserName then Email (asc).
    /// </summary>
    public UserListSortBy? SortBy { get; }

    public bool SortDescending { get; }
    public DateTime? LastLoginFrom { get; }
    public DateTime? LastLoginTo { get; }
    public int Skip => (Page - 1) * PageSize;
}
