using Endatix.Core.Entities.Identity;
using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Result;

namespace Endatix.Core.UseCases.Identity.ListRoles;

/// <summary>
/// Query for listing permissions in the current tenant. Tenant filter is implicit.
/// </summary>
public sealed record ListPermissionsQuery : IQuery<Result<IReadOnlyList<PermissionListItem>>>;
