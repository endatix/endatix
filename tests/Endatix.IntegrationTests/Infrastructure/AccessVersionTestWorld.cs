using Endatix.Core.Abstractions.Authorization;
using Endatix.Core.Entities.Identity;
using Endatix.Infrastructure.Identity;
using Endatix.Infrastructure.Identity.Authorization;
using Endatix.IntegrationTests.Shared;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Endatix.IntegrationTests;

/// <summary>The world and the version reads the access version tests share.</summary>
internal static class AccessVersionTestWorld
{
    public const string SEED_PASSWORD = "Password123!";
    public const string ROLE_NAME = "VersionedRole";
    public const string USER_NAME = "versioned@example.com";
    public const long SYSTEM_TENANT = AccessVersion.SYSTEM_TENANT_ID;
    public const long ANY_TENANT = AccessVersion.ANY_TENANT_ID;

    /// <summary>A one-tenant world with the test role (one permission) and the test user, who holds no role yet.</summary>
    public static async Task<IntegrationTestWorld> PrepareAsync(EndatixIntegrationWebHostFixture fixture, CancellationToken ct)
    {
        var world = await fixture.PrepareWorldAsync(
            IntegrationWorldOptions.SingleTenant with { DefaultPassword = SEED_PASSWORD }, ct);
        var tenantId = world.Tenants[0].Id;
        IntegrationSeedBuilder builder = new(world.Services);
        await builder.SeedRoleAsync(tenantId, ROLE_NAME, ct);
        await builder.SeedRolePermissionsAsync(tenantId, ROLE_NAME, [Actions.Forms.View], ct);
        await builder.SeedUserAsync(tenantId, USER_NAME, USER_NAME, password: SEED_PASSWORD, cancellationToken: ct);
        return world;
    }

    /// <summary>A role permission of the test role, which belongs to the world's tenant.</summary>
    public static Task<RolePermission> TenantRolePermissionAsync(AppIdentityDbContext db, IntegrationTestWorld world, CancellationToken ct)
    {
        var tenantId = world.Tenants[0].Id;
        var roleId = db.Roles.Where(r => r.TenantId == tenantId && r.Name == ROLE_NAME).Select(r => r.Id);
        return db.RolePermissions.FirstAsync(rp => roleId.Contains(rp.RoleId), ct);
    }

    /// <summary>The versions a request in <paramref name="tenantId"/> keys on, read the way the cache reads them.</summary>
    public static async Task<AccessVersionKey> ReadVersionsAsync(IntegrationTestWorld world, long tenantId, CancellationToken ct)
    {
        using var scope = world.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<IAccessVersionStore>().ReadAsync(tenantId, ct);
    }

    /// <summary>The world tenant's version, read the way the cache reads it.</summary>
    public static async Task<Guid> TenantVersionAsync(IntegrationTestWorld world, CancellationToken ct) =>
        (await ReadVersionsAsync(world, world.Tenants[0].Id, ct)).Scope;

    /// <summary>Removes a tenant's version row, as for a tenant whose authorization inputs never changed.</summary>
    public static async Task DeleteRowAsync(IntegrationTestWorld world, long tenantId, CancellationToken ct)
    {
        using var scope = world.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppIdentityDbContext>();
        await db.AccessVersions.Where(v => v.TenantId == tenantId).ExecuteDeleteAsync(ct);
    }

    /// <summary>The stored version of each row; <see cref="AccessVersion.Initial"/> when the row is missing.</summary>
    public static async Task<Dictionary<long, Guid>> RowsAsync(IntegrationTestWorld world, CancellationToken ct, params long[] tenantIds)
    {
        using var scope = world.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppIdentityDbContext>();
        var rows = await db.AccessVersions.AsNoTracking()
            .Where(v => tenantIds.Contains(v.TenantId))
            .ToDictionaryAsync(v => v.TenantId, v => v.Value, ct);
        return tenantIds.ToDictionary(id => id, id => rows.GetValueOrDefault(id, AccessVersion.Initial));
    }

    /// <summary>True when the row is stored.</summary>
    public static async Task<bool> RowExistsAsync(IntegrationTestWorld world, long tenantId, CancellationToken ct)
    {
        using var scope = world.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppIdentityDbContext>();
        return await db.AccessVersions.AnyAsync(v => v.TenantId == tenantId, ct);
    }
}
