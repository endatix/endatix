using Microsoft.AspNetCore.Authorization;

namespace Endatix.Infrastructure.Identity.Authorization;

/// <summary>
/// Requirement for platform-level administrator authorization.
/// Must be satisfied by the user to access cross-tenant operations.
/// </summary>
public sealed class PlatformAdminRequirement : IAuthorizationRequirement
{

}