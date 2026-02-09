using Endatix.Core.Entities.Identity;
using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Result;

namespace Endatix.Core.UseCases.Identity.ListUsers;

/// <summary>
/// Query for listing users in the current tenant. Tenant filter is implicit.
/// </summary>
public record ListUsersQuery(int? Page, int? PageSize) : IQuery<Result<IEnumerable<UserWithRoles>>>;
