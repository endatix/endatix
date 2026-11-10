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
/// <see cref="AppIdentityDbContext"/> changes the <see cref="AccessVersion"/> in the same save as a change to
/// an authorization input, and leaves it alone otherwise.
/// </summary>
[Collection(nameof(EndatixIntegrationTestCollection))]
[Trait("Category", "Infrastructure")]
[Trait("Priority", "P1")]
public sealed class AccessVersionTests(EndatixIntegrationWebHostFixture fixture)
{
    private const string SeedPassword = "Password123!";
    private const string RoleName = "VersionedRole";
    private const string UserName = "versioned@example.com";

    [Fact]
    public async Task SaveChangesAsync_UserRoleAdded_ChangesAccessVersion()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var world = await PrepareAsync(ct);
        var before = await ReadVersionAsync(world, ct);

        // Act
        IntegrationSeedBuilder builder = new(world.Services);
        await builder.SeedUserRoleAssignmentAsync(world.Tenants[0].Id, UserName, RoleName, ct);

        // Assert
        (await ReadVersionAsync(world, ct)).Should().NotBe(before);
    }

    [Fact]
    public async Task SaveChanges_RolePermissionRemoved_ChangesAccessVersion()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var world = await PrepareAsync(ct);
        var before = await ReadVersionAsync(world, ct);

        // Act
        using (var scope = world.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppIdentityDbContext>();
            var rolePermission = await db.RolePermissions.FirstAsync(ct);
            db.RolePermissions.Remove(rolePermission);
            db.SaveChanges();
        }

        // Assert
        (await ReadVersionAsync(world, ct)).Should().NotBe(before);
    }

    [Fact]
    public async Task SaveChangesAsync_UserProfileChanged_KeepsAccessVersion()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var world = await PrepareAsync(ct);
        var before = await ReadVersionAsync(world, ct);

        // Act
        using (var scope = world.Services.CreateScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
            var user = (await users.FindByNameAsync(UserName))!;
            user.PhoneNumber = "+10000000000";
            (await users.UpdateAsync(user)).Succeeded.Should().BeTrue();
        }

        // Assert
        (await ReadVersionAsync(world, ct)).Should().Be(before);
    }

    [Fact]
    public async Task SaveChangesAsync_NewUserGetsFirstRole_KeepsAccessVersion()
    {
        // Arrange — self-registration and invitations create the user and grant a role in one scope; a user created
        // there holds no token, so nothing cached can be stale.
        var ct = TestContext.Current.CancellationToken;
        var world = await PrepareAsync(ct);
        var before = await ReadVersionAsync(world, ct);

        // Act
        using (var scope = world.Services.CreateScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
            AppUser user = new() { UserName = "new@example.com", Email = "new@example.com", TenantId = world.Tenants[0].Id };
            (await users.CreateAsync(user)).Succeeded.Should().BeTrue();
            (await users.AddToRoleAsync(user, RoleName)).Succeeded.Should().BeTrue();
        }

        // Assert
        (await ReadVersionAsync(world, ct)).Should().Be(before);
    }

    [Fact]
    public async Task Commit_AfterRoleChangeTransactionDisposedWithoutCommit_KeepsAccessVersion()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var world = await PrepareAsync(ct);
        var before = await ReadVersionAsync(world, ct);
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
        (await ReadVersionAsync(world, ct)).Should().Be(before);
        (await db.RolePermissions.AsNoTracking().CountAsync(ct)).Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task DeleteRole_ThroughUnitOfWorkTransaction_ChangesAccessVersion()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var world = await PrepareAsync(ct);
        var before = await ReadVersionAsync(world, ct);

        // Act
        using (var scope = world.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppIdentityDbContext>();
            var roles = scope.ServiceProvider.GetRequiredService<IRolesRepository>();
            var role = await db.Roles.SingleAsync(r => r.Name == RoleName, ct);
            await roles.DeleteRoleAsync(role, ct);
        }

        // Assert
        (await ReadVersionAsync(world, ct)).Should().NotBe(before);
    }

    [Fact]
    public async Task SaveChangesAsync_WithRetryingExecutionStrategy_ChangesAccessVersion()
    {
        // Arrange — a host may enable retries on its provider, which refuses transactions opened outside the
        // execution strategy.
        var ct = TestContext.Current.CancellationToken;
        var world = await PrepareAsync(ct);
        var before = await ReadVersionAsync(world, ct);
        await using var db = new AppIdentityDbContext(RetryingOptions());
        db.RolePermissions.Remove(await db.RolePermissions.FirstAsync(ct));

        // Act
        await db.SaveChangesAsync(ct);

        // Assert
        (await ReadVersionAsync(world, ct)).Should().NotBe(before);
    }

    [Fact]
    public async Task SaveChanges_WithRetryingExecutionStrategy_ChangesAccessVersion()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var world = await PrepareAsync(ct);
        var before = await ReadVersionAsync(world, ct);
        using var db = new AppIdentityDbContext(RetryingOptions());
        db.RolePermissions.Remove(await db.RolePermissions.FirstAsync(ct));

        // Act
        db.SaveChanges();

        // Assert
        (await ReadVersionAsync(world, ct)).Should().NotBe(before);
    }

    [Fact]
    public async Task SaveChangesAsync_UserTenantChanged_ChangesAccessVersion()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var world = await PrepareAsync(ct);
        var before = await ReadVersionAsync(world, ct);

        // Act — removing a user's tenant access moves the user to tenant 0.
        using (var scope = world.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppIdentityDbContext>();
            var user = await db.Users.SingleAsync(u => u.UserName == UserName, ct);
            user.TenantId = 0;
            await db.SaveChangesAsync(ct);
        }

        // Assert
        (await ReadVersionAsync(world, ct)).Should().NotBe(before);
    }

    [Fact]
    public async Task SaveChangesAsync_UserDeleted_ChangesAccessVersion()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var world = await PrepareAsync(ct);
        var before = await ReadVersionAsync(world, ct);

        // Act
        using (var scope = world.Services.CreateScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
            var user = (await users.FindByNameAsync(UserName))!;
            (await users.DeleteAsync(user)).Succeeded.Should().BeTrue();
        }

        // Assert
        (await ReadVersionAsync(world, ct)).Should().NotBe(before);
    }

    [Fact]
    public async Task Commit_RetriedAfterAccessVersionUpdateFailed_WritesAccessVersion()
    {
        // Arrange — the system row is missing when the role change first commits, so that commit fails.
        var ct = TestContext.Current.CancellationToken;
        var world = await PrepareAsync(ct);
        var restored = Guid.NewGuid();
        using var scope = world.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppIdentityDbContext>();
        try
        {
            await DeleteSystemRowAsync(world, ct);
            await using var roleChange = await db.Database.BeginTransactionAsync(ct);
            db.RolePermissions.Remove(await db.RolePermissions.FirstAsync(ct));
            await db.SaveChangesAsync(ct);
            var firstCommit = () => roleChange.CommitAsync(ct);
            await firstCommit.Should().ThrowAsync<InvalidOperationException>();
            // Restored on the same connection: on SQL Server without READ_COMMITTED_SNAPSHOT, a second connection
            // would wait for this open transaction.
            db.AccessVersions.Add(new AccessVersion { TenantId = AccessVersion.SYSTEM_TENANT_ID, Value = restored });
            await db.SaveChangesAsync(ct);

            // Act
            await roleChange.CommitAsync(ct);

            // Assert
            (await ReadVersionAsync(world, ct)).Should().NotBe(restored);
        }
        finally
        {
            await RestoreSystemRowAsync(world, AccessVersion.Initial, CancellationToken.None);
        }
    }

    [Fact]
    public async Task Commit_ClearsUsersAddedInTheTransaction()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var world = await PrepareAsync(ct);
        using var scope = world.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppIdentityDbContext>();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        AppUser user = new() { UserName = "added@example.com", Email = "added@example.com", TenantId = world.Tenants[0].Id };
        (await users.CreateAsync(user)).Succeeded.Should().BeTrue();
        db.UsersAddedInTransaction.Should().Contain(user.Id);

        // Act
        await transaction.CommitAsync(ct);

        // Assert
        db.UsersAddedInTransaction.Should().BeEmpty();
    }

    [Fact]
    public async Task Rollback_ClearsUsersAddedInTheTransaction()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var world = await PrepareAsync(ct);
        using var scope = world.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppIdentityDbContext>();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        AppUser user = new() { UserName = "dropped@example.com", Email = "dropped@example.com", TenantId = world.Tenants[0].Id };
        (await users.CreateAsync(user)).Succeeded.Should().BeTrue();
        db.UsersAddedInTransaction.Should().Contain(user.Id);

        // Act
        await transaction.RollbackAsync(ct);

        // Assert
        db.UsersAddedInTransaction.Should().BeEmpty();
    }

    private static async Task DeleteSystemRowAsync(IntegrationTestWorld world, CancellationToken ct)
    {
        using var scope = world.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppIdentityDbContext>();
        await db.AccessVersions.Where(v => v.TenantId == AccessVersion.SYSTEM_TENANT_ID).ExecuteDeleteAsync(ct);
    }

    private static async Task RestoreSystemRowAsync(IntegrationTestWorld world, Guid value, CancellationToken ct)
    {
        using var scope = world.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppIdentityDbContext>();
        await db.AccessVersions.Where(v => v.TenantId == AccessVersion.SYSTEM_TENANT_ID).ExecuteDeleteAsync(ct);
        db.AccessVersions.Add(new AccessVersion { TenantId = AccessVersion.SYSTEM_TENANT_ID, Value = value });
        await db.SaveChangesAsync(ct);
    }

    private DbContextOptions<AppIdentityDbContext> RetryingOptions()
    {
        var builder = new DbContextOptionsBuilder<AppIdentityDbContext>();
        if (fixture.Database.Provider == TestDatabaseProvider.SqlServer)
        {
            builder.UseSqlServer(fixture.Database.ConnectionString, sql => sql.EnableRetryOnFailure());
        }
        else
        {
            builder.UseNpgsql(fixture.Database.ConnectionString, npgsql => npgsql.EnableRetryOnFailure());
        }

        return builder.Options;
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

    private static async Task<Guid> ReadVersionAsync(IntegrationTestWorld world, CancellationToken ct)
    {
        using var scope = world.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<IAccessVersionReader>().ReadAsync(ct);
    }
}
