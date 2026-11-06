using Endatix.Core.Abstractions.Authorization;
using Endatix.Infrastructure.Identity;
using Endatix.Infrastructure.Identity.Authorization;
using Endatix.Infrastructure.Identity.Repositories;
using Endatix.IntegrationTests.Shared;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Endatix.IntegrationTests;

/// <summary>
/// <see cref="AppIdentityDbContext"/> changes the <see cref="AuthorizationStamp"/> in the same save as a change to
/// an authorization input, and leaves it alone otherwise.
/// </summary>
[Collection(nameof(EndatixIntegrationTestCollection))]
[Trait("Category", "Infrastructure")]
[Trait("Priority", "P1")]
public sealed class AuthorizationStampTests(EndatixIntegrationWebHostFixture fixture)
{
    private const string SeedPassword = "Password123!";
    private const string RoleName = "StampedRole";
    private const string UserName = "stamped@example.com";

    [Fact]
    public async Task SaveChangesAsync_UserRoleAdded_ChangesStamp()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var world = await PrepareAsync(ct);
        var before = await ReadStampAsync(world, ct);

        // Act
        IntegrationSeedBuilder builder = new(world.Services);
        await builder.SeedUserRoleAssignmentAsync(world.Tenants[0].Id, UserName, RoleName, ct);

        // Assert
        (await ReadStampAsync(world, ct)).Should().NotBe(before);
    }

    [Fact]
    public async Task SaveChanges_RolePermissionRemoved_ChangesStamp()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var world = await PrepareAsync(ct);
        var before = await ReadStampAsync(world, ct);

        // Act
        using (var scope = world.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppIdentityDbContext>();
            var rolePermission = await db.RolePermissions.FirstAsync(ct);
            db.RolePermissions.Remove(rolePermission);
            db.SaveChanges();
        }

        // Assert
        (await ReadStampAsync(world, ct)).Should().NotBe(before);
    }

    [Fact]
    public async Task SaveChangesAsync_UserProfileChanged_KeepsStamp()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var world = await PrepareAsync(ct);
        var before = await ReadStampAsync(world, ct);

        // Act
        using (var scope = world.Services.CreateScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
            var user = (await users.FindByNameAsync(UserName))!;
            user.PhoneNumber = "+10000000000";
            (await users.UpdateAsync(user)).Succeeded.Should().BeTrue();
        }

        // Assert
        (await ReadStampAsync(world, ct)).Should().Be(before);
    }

    [Fact]
    public async Task SaveChangesAsync_NewUserGetsFirstRole_KeepsStamp()
    {
        // Arrange — self-registration and invitations create the user and grant a role in one scope; a user created
        // there holds no token, so nothing cached can be stale.
        var ct = TestContext.Current.CancellationToken;
        var world = await PrepareAsync(ct);
        var before = await ReadStampAsync(world, ct);

        // Act
        using (var scope = world.Services.CreateScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
            AppUser user = new() { UserName = "new@example.com", Email = "new@example.com", TenantId = world.Tenants[0].Id };
            (await users.CreateAsync(user)).Succeeded.Should().BeTrue();
            (await users.AddToRoleAsync(user, RoleName)).Succeeded.Should().BeTrue();
        }

        // Assert
        (await ReadStampAsync(world, ct)).Should().Be(before);
    }

    [Fact]
    public async Task Commit_AfterRoleChangeTransactionDisposedWithoutCommit_KeepsStamp()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var world = await PrepareAsync(ct);
        var before = await ReadStampAsync(world, ct);
        using var scope = world.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppIdentityDbContext>();

        // Act — a role change saved in a transaction that is dropped, then an unrelated save on the same context.
        await using (var abandoned = await db.Database.BeginTransactionAsync(ct))
        {
            db.RolePermissions.Remove(await db.RolePermissions.FirstAsync(ct));
            await db.SaveChangesAsync(ct);
        }

        db.ChangeTracker.Clear();
        var user = await db.Users.SingleAsync(u => u.UserName == UserName, ct);
        user.PhoneNumber = "+10000000001";
        await using (var unrelated = await db.Database.BeginTransactionAsync(ct))
        {
            await db.SaveChangesAsync(ct);
            await unrelated.CommitAsync(ct);
        }

        // Assert
        (await ReadStampAsync(world, ct)).Should().Be(before);
        (await db.RolePermissions.AsNoTracking().CountAsync(ct)).Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task DeleteRole_ThroughUnitOfWorkTransaction_ChangesStamp()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var world = await PrepareAsync(ct);
        var before = await ReadStampAsync(world, ct);

        // Act
        using (var scope = world.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppIdentityDbContext>();
            var roles = scope.ServiceProvider.GetRequiredService<IRolesRepository>();
            var role = await db.Roles.SingleAsync(r => r.Name == RoleName, ct);
            await roles.DeleteRoleAsync(role, ct);
        }

        // Assert
        (await ReadStampAsync(world, ct)).Should().NotBe(before);
    }

    private async Task<IntegrationTestWorld> PrepareAsync(CancellationToken ct)
    {
        var world = await fixture.PrepareWorldAsync(
            IntegrationWorldOptions.SingleTenant with { DefaultPassword = SeedPassword }, ct);
        var tenantId = world.Tenants[0].Id;
        IntegrationSeedBuilder builder = new(world.Services);
        await builder.SeedRoleAsync(tenantId, RoleName, ct);
        await builder.SeedRolePermissionsAsync(tenantId, RoleName, [Actions.Forms.View], ct);
        await builder.SeedUserAsync(tenantId, UserName, UserName, password: SeedPassword, cancellationToken: ct);
        return world;
    }

    private static async Task<Guid> ReadStampAsync(IntegrationTestWorld world, CancellationToken ct)
    {
        using var scope = world.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<IAuthorizationStampReader>().ReadAsync(ct);
    }
}
