using Endatix.Core.Infrastructure.Paging;

namespace Endatix.Core.UseCases.Identity.ListUsers;

/// <summary>
/// Domain filters and sort for user list queries. Paging and free-text search travel
/// separately in <see cref="SearchablePageRequest"/>, matching the
/// "Core paging + feature criteria" shape used by the other list read models.
/// </summary>
public sealed record UserListCriteria
{
    /// <summary>
    /// Creates user list criteria, normalizing the role and status filters.
    /// </summary>
    /// <param name="role">Role name filter.</param>
    /// <param name="status">Status filter: <c>active</c>, <c>pending</c>, or <c>locked</c>.</param>
    /// <param name="sort">Sort field and direction. When null, default order is UserName then Email ascending.</param>
    /// <param name="lastLogin">Last-login UTC bounds.</param>
    public UserListCriteria(
        string? role = null,
        string? status = null,
        SortRequest<UserListSortBy>? sort = null,
        UtcDateTimeRange lastLogin = default)
    {
        Role = Normalize(role);
        Status = Normalize(status)?.ToLowerInvariant();
        Sort = sort;
        LastLogin = lastLogin;
    }

    /// <summary>
    /// Role name filter, trimmed; null when not supplied.
    /// </summary>
    public string? Role { get; }

    /// <summary>
    /// Status filter (<c>active</c>, <c>pending</c>, or <c>locked</c>), trimmed and lowercased; null when not supplied.
    /// </summary>
    public string? Status { get; }

    /// <summary>
    /// Sort field and direction. When null, the read model keeps its default order.
    /// </summary>
    public SortRequest<UserListSortBy>? Sort { get; }

    /// <summary>
    /// Inclusive-from / exclusive-to last-login bounds.
    /// </summary>
    public UtcDateTimeRange LastLogin { get; }

    private static string? Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
