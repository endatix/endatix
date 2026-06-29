namespace Endatix.Infrastructure.Identity.Authorization;

/// <summary>
/// Enforces local PlatformAdmin approval for externally mapped roles.
/// </summary>
internal interface IPlatformAdminLocalApprovalGate
{
    /// <summary>
    /// Returns mapped roles and permissions after applying local PlatformAdmin approval rules.
    /// </summary>
    Task<PlatformAdminFilteredAuthorization> ApplyAsync(
        long tenantId,
        string authProvider,
        string externalSubjectId,
        long? userId,
        string[] mappedRoles,
        string[] mappedPermissions,
        CancellationToken cancellationToken);
}

/// <summary>
/// Roles and permissions effective after PlatformAdmin local-approval filtering.
/// </summary>
internal sealed record PlatformAdminFilteredAuthorization(string[] Roles, string[] Permissions);
