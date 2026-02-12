using Endatix.Api.Common;

namespace Endatix.Api.Endpoints.Users;

/// <summary>
/// Request for listing users in the current tenant. Tenant filter is implicit.
/// </summary>
public record ListUsersRequest : IPagedRequest
{
    /// <inheritdoc />
    public int? Page { get; set; }

    /// <inheritdoc />
    public int? PageSize { get; set; }
}
