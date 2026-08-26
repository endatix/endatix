using Endatix.Core.Entities.Identity;
using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Paging;
using Endatix.Core.Infrastructure.Result;

namespace Endatix.Core.UseCases.Identity.ListUsers;

/// <summary>
/// Query for listing users in the current tenant. Tenant filter is implicit.
/// Paging/search and the domain filters are normalized by the value objects themselves.
/// </summary>
/// <param name="Paging">Page, page size and free-text search.</param>
/// <param name="Criteria">Role/status filters, sort and last-login bounds.</param>
public sealed record ListUsersQuery(
    SearchablePageRequest Paging,
    UserListCriteria Criteria) : IQuery<Result<Paged<UserWithRoles>>>;
