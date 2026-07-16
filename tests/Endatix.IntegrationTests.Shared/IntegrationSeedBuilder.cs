using Endatix.Core.Entities;
using Endatix.Core.Abstractions;
using Endatix.Core.Abstractions.Authorization;
using Endatix.Core.Entities.Identity;
using Endatix.Infrastructure.Data;
using Endatix.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Endatix.IntegrationTests.Shared;

/// <summary>
/// Lightweight per-test seed helper for common integration data setup.
/// </summary>
/// <remarks>
/// Initialises the seed builder with the host's service provider.
/// </remarks>
public sealed class IntegrationSeedBuilder(IServiceProvider services)
{
    private readonly IServiceProvider _services = services;

    /// <summary>
    /// Seeds a tenant if one with the given name does not already exist.
    /// </summary>
    /// <returns>The tenant id (existing or newly created).</returns>
    public async Task<long> SeedTenantAsync(
        string tenantName,
        string? description = null,
        CancellationToken cancellationToken = default)
    {
        using var scope = _services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var existing = await db.Set<Tenant>()
            .FirstOrDefaultAsync(x => x.Name == tenantName, cancellationToken);
        if (existing is not null)
        {
            await SeedDefaultExportFormatsIfAvailableAsync(scope.ServiceProvider, existing.Id, cancellationToken);
            return existing.Id;
        }

        Tenant tenant = new(tenantName, description);
        db.Set<Tenant>().Add(tenant);
        await db.SaveChangesAsync(cancellationToken);

        await SeedDefaultExportFormatsIfAvailableAsync(scope.ServiceProvider, tenant.Id, cancellationToken);

        return tenant.Id;
    }

    private static async Task SeedDefaultExportFormatsIfAvailableAsync(
        IServiceProvider scopedServices,
        long tenantId,
        CancellationToken cancellationToken)
    {
        var seederType = Type.GetType(
            "Endatix.Modules.Reporting.Features.ExportFormats.IDefaultExportFormatsSeeder, Endatix.Modules.Reporting");
        if (seederType is null)
        {
            return;
        }

        var seeder = scopedServices.GetService(seederType);
        if (seeder is null)
        {
            return;
        }

        var seedMethod = seederType.GetMethod("SeedAsync");
        if (seedMethod is null)
        {
            return;
        }

        var task = seedMethod.Invoke(seeder, [tenantId, cancellationToken]) as Task;
        if (task is not null)
        {
            await task;
        }
    }

    /// <summary>
    /// Seeds a public, disabled form for the given tenant.
    /// </summary>
    public Task SeedFormAsync(long tenantId, string name, CancellationToken cancellationToken = default) => SeedFormAsync(tenantId, name, isPublic: true, isEnabled: false, cancellationToken);

    /// <summary>
    /// Seeds a form with explicit visibility and enabled state for the given tenant.
    /// </summary>
    public async Task SeedFormAsync(
        long tenantId,
        string name,
        bool isPublic,
        bool isEnabled,
        CancellationToken cancellationToken = default)
    {
        using var scope = _services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var exists = await db.Forms.AnyAsync(x => x.TenantId == tenantId && x.Name == name, cancellationToken);
        if (exists)
        {
            return;
        }

        Form form = new(tenantId, name, isEnabled: isEnabled, isPublic: isPublic);
        db.Forms.Add(form);
        await db.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Seeds a role for the given tenant if it does not already exist.
    /// </summary>
    public async Task SeedRoleAsync(long tenantId, string roleName, CancellationToken cancellationToken = default)
    {
        using var scope = _services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppIdentityDbContext>();

        var exists = await db.Roles.AnyAsync(x => x.TenantId == tenantId && x.Name == roleName, cancellationToken);
        if (exists)
        {
            return;
        }

        AppRole role = new()
        {
            TenantId = tenantId,
            Name = roleName,
            NormalizedName = roleName.ToUpperInvariant(),
            IsActive = true
        };

        db.Roles.Add(role);
        await db.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Seeds a user for the given tenant if one with the same user name does not already exist.
    /// When <paramref name="password"/> is provided the user is created via <see cref="UserManager{TUser}"/>
    /// so the password is hashed; otherwise the user is added raw to the store.
    /// </summary>
    public async Task SeedUserAsync(
        long tenantId,
        string userName,
        string email,
        bool emailConfirmed = true,
        string? password = null,
        CancellationToken cancellationToken = default)
    {
        using var scope = _services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppIdentityDbContext>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();

        var exists = await db.Users.AnyAsync(x => x.TenantId == tenantId && x.UserName == userName, cancellationToken);
        if (exists)
        {
            return;
        }

        AppUser user = new()
        {
            TenantId = tenantId,
            UserName = userName,
            NormalizedUserName = userName.ToUpperInvariant(),
            Email = email,
            NormalizedEmail = email.ToUpperInvariant(),
            EmailConfirmed = emailConfirmed,
            SecurityStamp = Guid.NewGuid().ToString("N")
        };

        if (string.IsNullOrWhiteSpace(password))
        {
            db.Users.Add(user);
            await db.SaveChangesAsync(cancellationToken);
            return;
        }

        var createResult = await userManager.CreateAsync(user, password);
        if (!createResult.Succeeded)
        {
            var errors = string.Join("; ", createResult.Errors.Select(x => $"{x.Code}: {x.Description}"));
            throw new InvalidOperationException($"Failed to create seeded user '{email}'. Errors: {errors}");
        }
    }

    /// <summary>
    /// Assigns a role to a user for the given tenant. No-op if either the user, role, or assignment does not exist.
    /// </summary>
    public async Task SeedUserRoleAssignmentAsync(
        long tenantId,
        string userName,
        string roleName,
        CancellationToken cancellationToken = default)
    {
        using var scope = _services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppIdentityDbContext>();

        var user = await db.Users
            .FirstOrDefaultAsync(x => x.TenantId == tenantId && x.UserName == userName, cancellationToken);
        var role = await db.Roles
            .FirstOrDefaultAsync(x => x.TenantId == tenantId && x.Name == roleName, cancellationToken);

        if (user is null || role is null)
        {
            return;
        }

        var exists = await db.UserRoles
            .AnyAsync(x => x.UserId == user.Id && x.RoleId == role.Id, cancellationToken);
        if (exists)
        {
            return;
        }

        IdentityUserRole<long> userRole = new()
        {
            UserId = user.Id,
            RoleId = role.Id
        };
        db.UserRoles.Add(userRole);
        await db.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Seeds the given permission names for a role, creating the permissions if they do not yet exist.
    /// </summary>
    public async Task SeedRolePermissionsAsync(
        long tenantId,
        string roleName,
        IReadOnlyList<string> permissionNames,
        CancellationToken cancellationToken = default)
    {
        if (permissionNames.Count == 0)
        {
            return;
        }

        using var scope = _services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppIdentityDbContext>();
        var idGenerator = scope.ServiceProvider.GetRequiredService<IIdGenerator<long>>();

        var role = await db.Roles
            .FirstOrDefaultAsync(x => x.TenantId == tenantId && x.Name == roleName, cancellationToken);
        if (role is null)
        {
            return;
        }

        var distinctPermissionNames = permissionNames
            .Distinct(StringComparer.Ordinal)
            .ToList();

        var permissionsByName = await db.Permissions
            .Where(x => distinctPermissionNames.Contains(x.Name))
            .ToDictionaryAsync(x => x.Name, cancellationToken);

        var permissionsChanged = false;
        foreach (var permissionName in distinctPermissionNames)
        {
            if (permissionsByName.ContainsKey(permissionName))
            {
                continue;
            }

            var permission = Permission.CreateSystemPermission(
                permissionName,
                $"{permissionName} seeded permission",
                "System");
            permission.Id = idGenerator.CreateId();
            db.Permissions.Add(permission);
            permissionsByName[permissionName] = permission;
            permissionsChanged = true;
        }

        if (permissionsChanged)
        {
            await db.SaveChangesAsync(cancellationToken);
        }

        var permissionIds = permissionsByName.Values.Select(x => x.Id).ToList();
        var assignedPermissionIds = (await db.RolePermissions
            .Where(x => x.RoleId == role.Id && permissionIds.Contains(x.PermissionId))
            .Select(x => x.PermissionId)
            .ToListAsync(cancellationToken))
            .ToHashSet();

        var rolePermissionsChanged = false;
        foreach (var permission in permissionsByName.Values)
        {
            if (assignedPermissionIds.Contains(permission.Id))
            {
                continue;
            }

            RolePermission rolePermission = new(role.Id, permission.Id)
            {
                Id = idGenerator.CreateId()
            };
            db.RolePermissions.Add(rolePermission);
            rolePermissionsChanged = true;
        }

        if (rolePermissionsChanged)
        {
            await db.SaveChangesAsync(cancellationToken);
        }
    }

    /// <summary>
    /// Runs the full standard seed: tenants, system roles, users, role assignments, and sample forms.
    /// Optionally invokes an <paramref name="afterSeed"/> callback for custom post-seed logic.
    /// </summary>
    public async Task<StandardSeedResult> SeedStandardAsync(
        StandardSeedOptions? options = null,
        Func<IServiceProvider, StandardSeedResult, CancellationToken, Task>? afterSeed = null,
        CancellationToken cancellationToken = default)
    {
        var resolvedOptions = options ?? StandardSeedOptions.CreateDefault();
        List<SeededTenant> seededTenants = [];

        foreach (var tenant in resolvedOptions.Tenants)
        {
            var tenantId = await SeedTenantAsync(tenant.Name, cancellationToken: cancellationToken);

            if (resolvedOptions.IncludePersistedSystemRoles)
            {
                foreach (var systemRole in SystemRole.AllSystemRoles.Where(x => x.IsPersisted))
                {
                    await SeedRoleAsync(tenantId, systemRole.Name, cancellationToken);

                    if (resolvedOptions.IncludeRolePermissions)
                    {
                        await SeedRolePermissionsAsync(tenantId, systemRole.Name, systemRole.Permissions, cancellationToken);
                    }
                }
            }

            await SeedUserAsync(
                tenantId,
                tenant.AdminUserName,
                tenant.AdminEmail,
                password: resolvedOptions.DefaultPassword,
                cancellationToken: cancellationToken);
            if (resolvedOptions.IncludePersistedSystemRoles)
            {
                await SeedUserRoleAssignmentAsync(tenantId, tenant.AdminUserName, SystemRole.Admin.Name, cancellationToken);
            }

            await SeedUserAsync(
                tenantId,
                tenant.CreatorUserName,
                tenant.CreatorEmail,
                password: resolvedOptions.DefaultPassword,
                cancellationToken: cancellationToken);
            if (resolvedOptions.IncludePersistedSystemRoles)
            {
                await SeedUserRoleAssignmentAsync(tenantId, tenant.CreatorUserName, SystemRole.Creator.Name, cancellationToken);
            }

            await SeedUserAsync(
                tenantId,
                tenant.PlatformAdminUserName,
                tenant.PlatformAdminEmail,
                password: resolvedOptions.DefaultPassword,
                cancellationToken: cancellationToken);
            if (resolvedOptions.IncludePersistedSystemRoles)
            {
                await SeedUserRoleAssignmentAsync(tenantId, tenant.PlatformAdminUserName, SystemRole.PlatformAdmin.Name, cancellationToken);
            }

            await SeedFormAsync(tenantId, $"{tenant.Name}-public-form", isPublic: true, isEnabled: true, cancellationToken);
            await SeedFormAsync(tenantId, $"{tenant.Name}-private-form", isPublic: false, isEnabled: true, cancellationToken);

            var admin = await ResolveSeededUserAsync(tenantId, tenant.AdminUserName, cancellationToken);
            var creator = await ResolveSeededUserAsync(tenantId, tenant.CreatorUserName, cancellationToken);
            var platformAdmin = await ResolveSeededUserAsync(tenantId, tenant.PlatformAdminUserName, cancellationToken);
            seededTenants.Add(new SeededTenant(tenantId, tenant.Name, admin, creator, platformAdmin));
        }

        StandardSeedResult result = new(seededTenants);
        if (afterSeed is not null)
        {
            await afterSeed(_services, result, cancellationToken);
        }

        return result;
    }

    private async Task<SeededUser> ResolveSeededUserAsync(long tenantId, string userName, CancellationToken cancellationToken)
    {
        using var scope = _services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppIdentityDbContext>();
        var user = await db.Users
            .FirstAsync(x => x.TenantId == tenantId && x.UserName == userName, cancellationToken);

        return new SeededUser(user.Id, user.UserName!, user.Email!, tenantId);
    }
}

/// <summary>
/// Options for the standard integration test seed run.
/// </summary>
public sealed record StandardSeedOptions(
    IReadOnlyList<StandardSeedTenant> Tenants,
    bool IncludePersistedSystemRoles = true,
    bool IncludeRolePermissions = true,
    string? DefaultPassword = null)
{
    /// <summary>
    /// Creates the default options with three seed tenants (a, b, c).
    /// </summary>
    public static StandardSeedOptions CreateDefault() => new StandardSeedOptions(
            [
                new StandardSeedTenant(
                    "seed-tenant-a",
                    "seed-admin-a",
                    "seed-admin-a@test.local",
                    "seed-creator-a",
                    "seed-creator-a@test.local",
                    "seed-platform-admin-a",
                    "seed-platform-admin-a@test.local"),
                new StandardSeedTenant(
                    "seed-tenant-b",
                    "seed-admin-b",
                    "seed-admin-b@test.local",
                    "seed-creator-b",
                    "seed-creator-b@test.local",
                    "seed-platform-admin-b",
                    "seed-platform-admin-b@test.local"),
                new StandardSeedTenant(
                    "seed-tenant-c",
                    "seed-admin-c",
                    "seed-admin-c@test.local",
                    "seed-creator-c",
                    "seed-creator-c@test.local",
                    "seed-platform-admin-c",
                    "seed-platform-admin-c@test.local")
            ]);
}

/// <summary>
/// Describes a tenant to seed during standard seed setup.
/// </summary>
public sealed record StandardSeedTenant(
    string Name,
    string AdminUserName,
    string AdminEmail,
    string CreatorUserName,
    string CreatorEmail,
    string PlatformAdminUserName,
    string PlatformAdminEmail);

/// <summary>
/// A user that was seeded as part of a standard seed run.
/// </summary>
public sealed record SeededUser(long Id, string UserName, string Email, long TenantId);

/// <summary>
/// A tenant that was seeded, including references to its admin, creator, and platform admin users.
/// </summary>
public sealed record SeededTenant(
    long Id,
    string Name,
    SeededUser Admin,
    SeededUser Creator,
    SeededUser PlatformAdmin);

/// <summary>
/// Result of a standard seed run, containing all seeded tenants.
/// </summary>
public sealed record StandardSeedResult(IReadOnlyList<SeededTenant> Tenants)
{
    /// <summary>
    /// Gets the ids of all seeded tenants.
    /// </summary>
    public IReadOnlyList<long> TenantIds => Tenants.Select(tenant => tenant.Id).ToList();
}
