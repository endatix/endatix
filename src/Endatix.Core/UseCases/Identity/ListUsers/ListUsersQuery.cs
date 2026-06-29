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

    /// <summary>
    /// Initializes a new instance of the <see cref="ListUsersQuery"/> class.
    /// </summary>
    /// <param name="page">The page number to retrieve.</param>
    /// <param name="pageSize">The number of items per page.</param>
    /// <param name="search">The search query.</param>
    /// <param name="role">The role to filter by.</param>
    /// <param name="status">The status to filter by.</param>
    public ListUsersQuery(int? page, int? pageSize, string? search, string? role, string? status)
    {
        Page = Math.Max(page ?? DefaultPage, DefaultPage);
        PageSize = Math.Clamp(pageSize ?? DefaultPageSize, 1, MaxPageSize);
        Search = string.IsNullOrWhiteSpace(search) ? null : search.Trim();
        Role = string.IsNullOrWhiteSpace(role) ? null : role.Trim();
        Status = string.IsNullOrWhiteSpace(status) ? null : status.Trim().ToLowerInvariant();
    }

    public int Page { get; }
    public int PageSize { get; }
    public string? Search { get; }
    public string? Role { get; }
    public string? Status { get; }
    public int Skip => (Page - 1) * PageSize;
}
