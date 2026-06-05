using Endatix.Core.Entities.Identity;
using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Result;

namespace Endatix.Core.UseCases.Identity.GetUserById;

/// <summary>
/// Query for retrieving a tenant user by ID. Tenant filter is implicit.
/// </summary>
public sealed record GetUserByIdQuery(long UserId) : IQuery<Result<UserWithRoles>>;
