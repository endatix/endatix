using Endatix.Core.Abstractions.Authorization;
using Endatix.Infrastructure.Identity;
using Endatix.Infrastructure.Identity.Authorization;
using Endatix.IntegrationTests.Shared;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using static Endatix.IntegrationTests.AccessVersionTestWorld;

namespace Endatix.IntegrationTests;

/// <summary>
/// Which <see cref="AccessVersion"/> rows a change moves: its own tenant's, the system row for system roles and
/// platform admins, and the row every change moves; and how a tenant without a row gets one.
/// </summary>
[Collection(nameof(EndatixIntegrationTestCollection))]
[Trait("Category", "Infrastructure")]
[Trait("Priority", "P1")]
public sealed class AccessVersionScopeTests(EndatixIntegrationWebHostFixture fixture)
{
    private const string SYSTEM_ROLE_NAME = "VersionedSystemRole";

    [Fact]
    public async Task SaveChangesAsync_RoleChangedInOneTenant_KeepsOtherTenantAndSystemVersions()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var world = await fixture.PrepareWorldAsync(IntegrationWorldOptions.MultiTenant with { DefaultPassword = SEED_PASSWORD }, ct);
        var (tenantA, tenantB) = (world.Tenants[0].Id, world.Tenants[1].Id);
        IntegrationSeedBuilder builder = new(world.Services);
        foreach (var tenantId in new[] { tenantA, tenantB })
        {
            await builder.SeedRoleAsync(tenantId, ROLE_NAME, ct);
            await builder.SeedRolePermissionsAsync(tenantId, ROLE_NAME, [Actions.Forms.View], ct);
        }

        var before = await RowsAsync(world, ct, tenantA, tenantB, SYSTEM_TENANT, ANY_TENANT);

        // Act
        await builder.SeedRolePermissionsAsync(tenantA, ROLE_NAME, [Actions.Forms.Create], ct);

        // Assert
        var after = await RowsAsync(world, ct, tenantA, tenantB, SYSTEM_TENANT, ANY_TENANT);
        after[tenantA].Should().NotBe(before[tenantA]);
        after[tenantB].Should().Be(before[tenantB]);
        after[SYSTEM_TENANT].Should().Be(before[SYSTEM_TENANT]);
        after[ANY_TENANT].Should().NotBe(before[ANY_TENANT], "principals without a tenant key on the version every change moves");
    }

    [Fact]
    public async Task SaveChangesAsync_SystemRolePermissionChanged_ChangesSystemVersion()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var world = await PrepareAsync(fixture, ct);
        IntegrationSeedBuilder builder = new(world.Services);
        await builder.SeedRoleAsync(SYSTEM_TENANT, SYSTEM_ROLE_NAME, ct);
        var before = await RowsAsync(world, ct, SYSTEM_TENANT);

        // Act
        await builder.SeedRolePermissionsAsync(SYSTEM_TENANT, SYSTEM_ROLE_NAME, [Actions.Forms.View], ct);

        // Assert
        (await RowsAsync(world, ct, SYSTEM_TENANT))[SYSTEM_TENANT].Should().NotBe(before[SYSTEM_TENANT]);
    }

    [Fact]
    public async Task SaveChangesAsync_FirstChangeInTenantWithoutVersionRow_InsertsRandomVersion()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var world = await PrepareAsync(fixture, ct);
        var tenantId = world.Tenants[0].Id;
        await DeleteRowAsync(world, tenantId, ct);
        (await RowExistsAsync(world, tenantId, ct)).Should().BeFalse();

        // Act
        IntegrationSeedBuilder builder = new(world.Services);
        await builder.SeedUserRoleAssignmentAsync(tenantId, USER_NAME, ROLE_NAME, ct);

        // Assert
        (await RowExistsAsync(world, tenantId, ct)).Should().BeTrue();
        (await RowsAsync(world, ct, tenantId))[tenantId].Should().NotBe(AccessVersion.Initial);
    }

    [Fact]
    public async Task SaveChangesAsync_PlatformAdminGetsATenantRole_ChangesSystemVersion()
    {
        // Arrange — a platform admin's assumed-tenant sessions key on other tenants' versions and the system one.
        var ct = TestContext.Current.CancellationToken;
        var world = await PrepareAsync(fixture, ct);
        long adminId;
        using (var scope = world.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppIdentityDbContext>();
            var platformAdmin = await db.Roles.SingleAsync(r => r.Name == SystemRole.PlatformAdmin.Name, ct);
            adminId = await db.UserRoles.Where(ur => ur.RoleId == platformAdmin.Id).Select(ur => ur.UserId).FirstAsync(ct);
        }

        var before = await RowsAsync(world, ct, SYSTEM_TENANT);

        // Act
        await AddUserRoleAsync(world, (adminId, r => r.TenantId == world.Tenants[0].Id && r.Name == ROLE_NAME), ct);

        // Assert
        (await RowsAsync(world, ct, SYSTEM_TENANT))[SYSTEM_TENANT].Should().NotBe(before[SYSTEM_TENANT]);
    }

    [Fact]
    public async Task SaveChangesAsync_UserGetsThePlatformAdminRole_ChangesSystemVersion()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var world = await PrepareAsync(fixture, ct);
        long userId;
        using (var scope = world.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppIdentityDbContext>();
            userId = await db.Users.Where(u => u.UserName == USER_NAME).Select(u => u.Id).SingleAsync(ct);
        }

        var before = await RowsAsync(world, ct, SYSTEM_TENANT);

        // Act
        await AddUserRoleAsync(world, (userId, r => r.Name == SystemRole.PlatformAdmin.Name), ct);

        // Assert
        (await RowsAsync(world, ct, SYSTEM_TENANT))[SYSTEM_TENANT].Should().NotBe(before[SYSTEM_TENANT]);
    }

    [Fact]
    public async Task SaveChangesAsync_PermissionChanged_ChangesSystemVersion()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var world = await PrepareAsync(fixture, ct);
        var before = await RowsAsync(world, ct, SYSTEM_TENANT);

        // Act
        using (var scope = world.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppIdentityDbContext>();
            var permission = await db.Permissions.FirstAsync(p => p.Name == Actions.Forms.View, ct);
            permission.UpdateDescription($"Changed {Guid.NewGuid():N}");
            await db.SaveChangesAsync(ct);
        }

        // Assert
        (await RowsAsync(world, ct, SYSTEM_TENANT))[SYSTEM_TENANT].Should().NotBe(before[SYSTEM_TENANT]);
    }

    [Fact]
    public async Task ReadAsync_SameDatabase_DeploymentPrefixIsRandomAndStable()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var world = await PrepareAsync(fixture, ct);
        var tenantId = world.Tenants[0].Id;
        var first = await ReadVersionsAsync(world, tenantId, ct);

        // Act
        IntegrationSeedBuilder builder = new(world.Services);
        await builder.SeedUserRoleAssignmentAsync(tenantId, USER_NAME, ROLE_NAME, ct);
        var second = await ReadVersionsAsync(world, tenantId, ct);

        // Assert
        first.Deployment.Should().NotBe(Guid.Empty);
        second.Deployment.Should().Be(first.Deployment);
        second.Scope.Should().NotBe(first.Scope);
    }

    /// <summary>Grants the user the role the predicate matches, through the context.</summary>
    private static async Task AddUserRoleAsync(IntegrationTestWorld world, (long UserId, System.Linq.Expressions.Expression<Func<AppRole, bool>> Role) grant, CancellationToken ct)
    {
        using var scope = world.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppIdentityDbContext>();
        var roleId = await db.Roles.Where(grant.Role).Select(r => r.Id).SingleAsync(ct);
        db.UserRoles.Add(new IdentityUserRole<long> { UserId = grant.UserId, RoleId = roleId });
        await db.SaveChangesAsync(ct);
    }
}
