using Endatix.Core.Abstractions;
using Endatix.Core.Abstractions.Authorization;
using Endatix.Core.Entities.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Endatix.Infrastructure.Identity.Seed;

/// <summary>
/// Idempotent catalog rows that EF identity migrations do not yet include.
/// </summary>
public static class IdentityCatalogSeed
{
    public static async Task EnsureAssumeTenantPermissionAsync(
        AppIdentityDbContext identityDbContext,
        IIdGenerator<long> idGenerator,
        ILogger logger,
        CancellationToken cancellationToken = default)
    {
        var permissionName = Actions.Platform.AssumeTenants;
        var permission = await identityDbContext.Permissions
            .FirstOrDefaultAsync(row => row.Name == permissionName, cancellationToken);
        if (permission is null)
        {
            permission = Permission.CreateSystemPermission(
                permissionName,
                "Assume a tenant session for support access without impersonating a user.",
                "platform");
            permission.Id = idGenerator.CreateId();
            identityDbContext.Permissions.Add(permission);
            await identityDbContext.SaveChangesAsync(cancellationToken);
            logger.LogInformation("Seeded permission {PermissionName}.", permissionName);
        }

        var platformAdmin = await identityDbContext.Roles
            .FirstOrDefaultAsync(role => role.Name == SystemRole.PlatformAdmin.Name, cancellationToken);
        if (platformAdmin is null)
        {
            return;
        }

        var alreadyGranted = await identityDbContext.RolePermissions.AnyAsync(
            rolePermission => rolePermission.RoleId == platformAdmin.Id && rolePermission.PermissionId == permission.Id,
            cancellationToken);
        if (alreadyGranted)
        {
            return;
        }

        var grant = new RolePermission(platformAdmin.Id, permission.Id)
        {
            Id = idGenerator.CreateId()
        };
        identityDbContext.RolePermissions.Add(grant);
        await identityDbContext.SaveChangesAsync(cancellationToken);
        logger.LogInformation(
            "Granted {PermissionName} to {RoleName}.",
            permissionName,
            SystemRole.PlatformAdmin.Name);
    }
}
