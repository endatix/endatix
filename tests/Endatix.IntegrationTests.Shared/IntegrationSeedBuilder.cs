using Endatix.Core.Entities;
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
public sealed class IntegrationSeedBuilder
{
    private readonly IServiceProvider _services;

    public IntegrationSeedBuilder(IServiceProvider services)
    {
        _services = services;
    }

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
            return existing.Id;
        }

        Tenant tenant = new(tenantName, description);
        db.Set<Tenant>().Add(tenant);
        await db.SaveChangesAsync(cancellationToken);
        return tenant.Id;
    }

    public Task SeedFormAsync(long tenantId, string name, CancellationToken cancellationToken = default)
    {
        return SeedFormAsync(tenantId, name, isPublic: true, isEnabled: false, cancellationToken);
    }

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

        var role = await db.Roles
            .FirstOrDefaultAsync(x => x.TenantId == tenantId && x.Name == roleName, cancellationToken);
        if (role is null)
        {
            return;
        }

        foreach (var permissionName in permissionNames)
        {
            var permission = await db.Permissions
                .FirstOrDefaultAsync(x => x.Name == permissionName, cancellationToken);
            if (permission is null)
            {
                permission = Permission.CreateSystemPermission(
                    permissionName,
                    $"{permissionName} seeded permission",
                    "System");
                db.Permissions.Add(permission);
                await db.SaveChangesAsync(cancellationToken);
            }

            var exists = await db.RolePermissions
                .AnyAsync(
                    x => x.RoleId == role.Id && x.PermissionId == permission.Id,
                    cancellationToken);
            if (exists)
            {
                continue;
            }

            RolePermission rolePermission = new(role.Id, permission.Id);
            db.RolePermissions.Add(rolePermission);
            await db.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task<StandardSeedResult> SeedStandardAsync(
        StandardSeedOptions? options = null,
        Func<IServiceProvider, StandardSeedResult, CancellationToken, Task>? afterSeed = null,
        CancellationToken cancellationToken = default)
    {
        var resolvedOptions = options ?? StandardSeedOptions.CreateDefault();
        List<long> tenantIds = [];

        foreach (var tenant in resolvedOptions.Tenants)
        {
            var tenantId = await SeedTenantAsync(tenant.Name, cancellationToken: cancellationToken);
            tenantIds.Add(tenantId);

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
            await SeedUserRoleAssignmentAsync(tenantId, tenant.AdminUserName, SystemRole.Admin.Name, cancellationToken);

            await SeedUserAsync(
                tenantId,
                tenant.CreatorUserName,
                tenant.CreatorEmail,
                password: resolvedOptions.DefaultPassword,
                cancellationToken: cancellationToken);
            await SeedUserRoleAssignmentAsync(tenantId, tenant.CreatorUserName, SystemRole.Creator.Name, cancellationToken);

            await SeedUserAsync(
                tenantId,
                tenant.PlatformAdminUserName,
                tenant.PlatformAdminEmail,
                password: resolvedOptions.DefaultPassword,
                cancellationToken: cancellationToken);
            await SeedUserRoleAssignmentAsync(tenantId, tenant.PlatformAdminUserName, SystemRole.PlatformAdmin.Name, cancellationToken);

            await SeedFormAsync(tenantId, $"{tenant.Name}-public-form", isPublic: true, isEnabled: true, cancellationToken);
            await SeedFormAsync(tenantId, $"{tenant.Name}-private-form", isPublic: false, isEnabled: true, cancellationToken);
        }

        StandardSeedResult result = new([.. tenantIds]);
        if (afterSeed is not null)
        {
            await afterSeed(_services, result, cancellationToken);
        }

        return result;
    }
}

public sealed record StandardSeedOptions(
    IReadOnlyList<StandardSeedTenant> Tenants,
    bool IncludePersistedSystemRoles = true,
    bool IncludeRolePermissions = true,
    string? DefaultPassword = null)
{
    public static StandardSeedOptions CreateDefault()
    {
        return new StandardSeedOptions(
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
}

public sealed record StandardSeedTenant(
    string Name,
    string AdminUserName,
    string AdminEmail,
    string CreatorUserName,
    string CreatorEmail,
    string PlatformAdminUserName,
    string PlatformAdminEmail);

public sealed record StandardSeedResult(IReadOnlyList<long> TenantIds);
