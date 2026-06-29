using Endatix.Core.Entities.Identity;
using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Result;

namespace Endatix.Core.UseCases.Identity.ListRoles;

/// <summary>
/// Query for listing roles in the current tenant. Tenant filter is implicit.
/// </summary>
public sealed record ListRolesQuery : IQuery<Result<Paged<RoleListItem>>>
{
    public const int DefaultPage = 1;
    public const int DefaultPageSize = 10;
    public const int MaxPageSize = 100;

    public ListRolesQuery(int? page, int? pageSize, string? roleType, string? search)
    {
        Page = Math.Max(page ?? DefaultPage, DefaultPage);
        PageSize = Math.Clamp(pageSize ?? DefaultPageSize, 1, MaxPageSize);
        RoleType = string.IsNullOrWhiteSpace(roleType) ? null : roleType.Trim().ToLowerInvariant();
        Search = string.IsNullOrWhiteSpace(search) ? null : search.Trim();
    }

    public int Page { get; }
    public int PageSize { get; }
    public string? RoleType { get; }
    public string? Search { get; }
    public int Skip => (Page - 1) * PageSize;
}
