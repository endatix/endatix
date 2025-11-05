using Microsoft.AspNetCore.Authorization;

namespace Endatix.Infrastructure.Identity.Authorization;

/// <summary>
/// Requirement for tenant-level administrator authorization.
/// Must be satisfied by the user to access tenant-level operations.
/// </summary>
public sealed class TenantAdminRequirement : IAuthorizationRequirement
{

}