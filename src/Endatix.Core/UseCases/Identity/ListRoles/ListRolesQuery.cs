using Endatix.Core.Entities.Identity;
using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Paging;
using Endatix.Core.Infrastructure.Result;

namespace Endatix.Core.UseCases.Identity.ListRoles;

/// <summary>
/// Query for listing roles in the current tenant. Tenant filter is implicit.
/// Paging/search and the role type filter are normalized by the value objects themselves.
/// </summary>
/// <param name="Paging">Page, page size and free-text search.</param>
/// <param name="Criteria">Role type filter and sort.</param>
public sealed record ListRolesQuery(
    SearchablePageRequest Paging,
    RoleListCriteria Criteria) : IQuery<Result<Paged<RoleListItem>>>;
