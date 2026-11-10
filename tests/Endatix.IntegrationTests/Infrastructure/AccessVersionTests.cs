using Endatix.Core.Abstractions.Authorization;
using Endatix.Core.Entities.Identity;
using Endatix.Infrastructure.Identity;
using Endatix.Infrastructure.Identity.Authorization;
using Endatix.Infrastructure.Identity.Repositories;
using Endatix.IntegrationTests.Shared;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Endatix.IntegrationTests;

/// <summary>
/// <see cref="AppIdentityDbContext"/> changes the <see cref="AccessVersion"/> of the tenant an authorization input
/// belongs to (system roles: the system version) in the same transaction as the change, and leaves every other
/// version alone.
/// </summary>
[Collection(nameof(EndatixIntegrationTestCollection))]
[Trait("Category", "Infrastructure")]
[Trait("Priority", "P1")]
public sealed class AccessVersionTests(EndatixIntegrationWebHostFixture fixture)
{
    private const string SEED_PASSWORD = "Password123!";
    private const string ROLE_NAME = "VersionedRole";
    private const string USER_NAME = "versioned@example.com";
    private const string SYSTEM_ROLE_NAME = "VersionedSystemRole";
    private const long SYSTEM_TENANT = AccessVersion.SYSTEM_TENANT_ID;
    private const long ANY_TENANT = AccessVersion.ANY_TENANT_ID;

    [Fact]
    public async Task SaveChangesAsync_UserRoleAdded_ChangesTenantVersionOnly()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var world = await PrepareAsync(ct);
        var tenantId = world.Tenants[0].Id;
        var before = await RowsAsync(world, ct, tenantId, SYSTEM_TENANT);

        // Act
        IntegrationSeedBuilder builder = new(world.Services);
        await builder.SeedUserRoleAssignmentAsync(tenantId, USER_NAME, ROLE_NAME, ct);

        // Assert
        var after = await RowsAsync(world, ct, tenantId, SYSTEM_TENANT);
        after[tenantId].Should().NotBe(before[tenantId]);
        after[SYSTEM_TENANT].Should().Be(before[SYSTEM_TENANT]);
    }

    [Fact]
    public async Task SaveChanges_RolePermissionRemoved_ChangesTenantVersion()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var world = await PrepareAsync(ct);
        var before = await TenantVersionAsync(world, ct);

        // Act
        using (var scope = world.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppIdentityDbContext>();
            db.RolePermissions.Remove(await TenantRolePermissionAsync(db, world, ct));
            db.SaveChanges();
        }

        // Assert
        (await TenantVersionAsync(world, ct)).Should().NotBe(before);
    }

    [Fact]
    public async Task SaveChangesAsync_UserProfileChanged_KeepsAccessVersions()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var world = await PrepareAsync(ct);
        var before = await TenantVersionAsync(world, ct);

        // Act
        using (var scope = world.Services.CreateScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
            var user = (await users.FindByNameAsync(USER_NAME))!;
            user.PhoneNumber = "+10000000000";
            (await users.UpdateAsync(user)).Succeeded.Should().BeTrue();
        }

        // Assert
        (await TenantVersionAsync(world, ct)).Should().Be(before);
    }

    [Fact]
    public async Task SaveChangesAsync_NewUserGetsFirstRoleInSameTransaction_KeepsAccessVersions()
    {
        // Arrange — self-registration creates the user and grants the first role in one transaction; a user created
        // in it holds no token yet, so nothing cached can be stale.
        var ct = TestContext.Current.CancellationToken;
        var world = await PrepareAsync(ct);
        var before = await TenantVersionAsync(world, ct);

        // Act
        using (var scope = world.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppIdentityDbContext>();
            var users = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
            await using var signup = await db.Database.BeginTransactionAsync(ct);
            AppUser user = new() { UserName = "new@example.com", Email = "new@example.com", TenantId = world.Tenants[0].Id };
            (await users.CreateAsync(user)).Succeeded.Should().BeTrue();
            (await users.AddToRoleAsync(user, ROLE_NAME)).Succeeded.Should().BeTrue();
            await signup.CommitAsync(ct);
        }

        // Assert
        (await TenantVersionAsync(world, ct)).Should().Be(before);
    }

    [Fact]
    public async Task SaveChangesAsync_UserCreatedInEarlierSaveGetsRole_ChangesTenantVersion()
    {
        // Arrange — a long-lived context (a job, an import) creates a user in one save without a transaction.
        var ct = TestContext.Current.CancellationToken;
        var world = await PrepareAsync(ct);
        using var scope = world.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
        AppUser user = new() { UserName = "later@example.com", Email = "later@example.com", TenantId = world.Tenants[0].Id };
        (await users.CreateAsync(user)).Succeeded.Should().BeTrue();
        var before = await TenantVersionAsync(world, ct);

        // Act — a later save on the same context grants the user a role.
        (await users.AddToRoleAsync(user, ROLE_NAME)).Succeeded.Should().BeTrue();

        // Assert
        (await TenantVersionAsync(world, ct)).Should().NotBe(before);
    }

    [Fact]
    public async Task Commit_AfterRoleChangeTransactionDisposedWithoutCommit_KeepsAccessVersions()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var world = await PrepareAsync(ct);
        var before = await TenantVersionAsync(world, ct);
        using var scope = world.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppIdentityDbContext>();

        // Act — a role change saved in a transaction that is dropped, then an unrelated save on the same context.
        await using (var abandoned = await db.Database.BeginTransactionAsync(ct))
        {
            db.RolePermissions.Remove(await TenantRolePermissionAsync(db, world, ct));
            await db.SaveChangesAsync(ct);
        }

        db.ChangeTracker.Clear();
        var user = await db.Users.SingleAsync(u => u.UserName == USER_NAME, ct);
        user.PhoneNumber = "+10000000001";
        await using (var unrelated = await db.Database.BeginTransactionAsync(ct))
        {
            await db.SaveChangesAsync(ct);
            await unrelated.CommitAsync(ct);
        }

        // Assert
        (await TenantVersionAsync(world, ct)).Should().Be(before);
        (await db.RolePermissions.AsNoTracking().CountAsync(ct)).Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task DeleteRole_ThroughUnitOfWorkTransaction_ChangesTenantVersion()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var world = await PrepareAsync(ct);
        var before = await TenantVersionAsync(world, ct);

        // Act
        using (var scope = world.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppIdentityDbContext>();
            var roles = scope.ServiceProvider.GetRequiredService<IRolesRepository>();
            var role = await db.Roles.SingleAsync(r => r.Name == ROLE_NAME, ct);
            await roles.DeleteRoleAsync(role, ct);
        }

        // Assert
        (await TenantVersionAsync(world, ct)).Should().NotBe(before);
    }

    [Fact]
    public async Task SaveChangesAsync_WithRetryingExecutionStrategy_ChangesTenantVersion()
    {
        // Arrange — a host may enable retries on its provider, which refuses transactions opened outside the
        // execution strategy.
        var ct = TestContext.Current.CancellationToken;
        var world = await PrepareAsync(ct);
        var before = await TenantVersionAsync(world, ct);
        await using var db = new AppIdentityDbContext(RetryingOptions());
        db.RolePermissions.Remove(await TenantRolePermissionAsync(db, world, ct));

        // Act
        await db.SaveChangesAsync(ct);

        // Assert
        (await TenantVersionAsync(world, ct)).Should().NotBe(before);
    }

    [Fact]
    public async Task SaveChanges_WithRetryingExecutionStrategy_ChangesTenantVersion()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var world = await PrepareAsync(ct);
        var before = await TenantVersionAsync(world, ct);
        using var db = new AppIdentityDbContext(RetryingOptions());
        db.RolePermissions.Remove(await TenantRolePermissionAsync(db, world, ct));

        // Act
        db.SaveChanges();

        // Assert
        (await TenantVersionAsync(world, ct)).Should().NotBe(before);
    }

    [Fact]
    public async Task SaveChangesAsync_UserTenantChanged_ChangesOldAndNewTenantVersions()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var world = await PrepareAsync(ct);
        var tenantId = world.Tenants[0].Id;
        var before = await RowsAsync(world, ct, tenantId, SYSTEM_TENANT);

        // Act — removing a user's tenant access moves the user to tenant 0.
        using (var scope = world.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppIdentityDbContext>();
            var user = await db.Users.SingleAsync(u => u.UserName == USER_NAME, ct);
            user.TenantId = 0;
            await db.SaveChangesAsync(ct);
        }

        // Assert
        var after = await RowsAsync(world, ct, tenantId, SYSTEM_TENANT);
        after[tenantId].Should().NotBe(before[tenantId]);
        after[SYSTEM_TENANT].Should().NotBe(before[SYSTEM_TENANT]);
    }

    [Fact]
    public async Task SaveChangesAsync_UserDeleted_ChangesTenantVersion()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var world = await PrepareAsync(ct);
        var before = await TenantVersionAsync(world, ct);

        // Act
        using (var scope = world.Services.CreateScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
            var user = (await users.FindByNameAsync(USER_NAME))!;
            (await users.DeleteAsync(user)).Succeeded.Should().BeTrue();
        }

        // Assert
        (await TenantVersionAsync(world, ct)).Should().NotBe(before);
    }

    [Fact]
    public async Task Commit_RetriedAfterAccessVersionWriteFailed_WritesAccessVersion()
    {
        // Arrange — the first access version write of the commit fails before it reaches the database.
        var ct = TestContext.Current.CancellationToken;
        var world = await PrepareAsync(ct);
        var before = await TenantVersionAsync(world, ct);
        FailFirstAccessVersionWrite fault = new();
        await using var db = new AppIdentityDbContext(ProviderOptions(builder => builder.AddInterceptors(fault)));
        await using var roleChange = await db.Database.BeginTransactionAsync(ct);
        db.RolePermissions.Remove(await TenantRolePermissionAsync(db, world, ct));
        await db.SaveChangesAsync(ct);
        var firstCommit = () => roleChange.CommitAsync(ct);
        await firstCommit.Should().ThrowAsync<InvalidOperationException>();

        // Act
        await roleChange.CommitAsync(ct);

        // Assert
        fault.Failed.Should().BeTrue();
        (await TenantVersionAsync(world, ct)).Should().NotBe(before);
    }

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
        var world = await PrepareAsync(ct);
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
        var world = await PrepareAsync(ct);
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
    public async Task SaveChangesAsync_PlatformAdminLosesARole_ChangesSystemVersion()
    {
        // Arrange — a platform admin's assumed-tenant sessions are keyed on other tenants' versions and the system one.
        var ct = TestContext.Current.CancellationToken;
        var world = await PrepareAsync(ct);
        long adminId;
        using (var scope = world.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppIdentityDbContext>();
            var platformAdmin = await db.Roles.SingleAsync(r => r.Name == SystemRole.PlatformAdmin.Name, ct);
            adminId = await db.UserRoles.Where(ur => ur.RoleId == platformAdmin.Id).Select(ur => ur.UserId).FirstAsync(ct);
        }

        var before = await RowsAsync(world, ct, SYSTEM_TENANT);

        // Act
        using (var scope = world.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppIdentityDbContext>();
            var role = await db.UserRoles.FirstAsync(ur => ur.UserId == adminId, ct);
            db.UserRoles.Remove(role);
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
        var world = await PrepareAsync(ct);
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

    /// <summary>Removes a tenant's version row, as for a tenant whose authorization inputs never changed.</summary>
    private static async Task DeleteRowAsync(IntegrationTestWorld world, long tenantId, CancellationToken ct)
    {
        using var scope = world.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppIdentityDbContext>();
        await db.AccessVersions.Where(v => v.TenantId == tenantId).ExecuteDeleteAsync(ct);
    }

    /// <summary>The stored version of each row; <see cref="AccessVersion.Initial"/> when the row is missing.</summary>
    private static async Task<Dictionary<long, Guid>> RowsAsync(IntegrationTestWorld world, CancellationToken ct, params long[] tenantIds)
    {
        using var scope = world.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppIdentityDbContext>();
        var rows = await db.AccessVersions.AsNoTracking()
            .Where(v => tenantIds.Contains(v.TenantId))
            .ToDictionaryAsync(v => v.TenantId, v => v.Value, ct);
        return tenantIds.ToDictionary(id => id, id => rows.GetValueOrDefault(id, AccessVersion.Initial));
    }

    private static async Task<bool> RowExistsAsync(IntegrationTestWorld world, long tenantId, CancellationToken ct)
    {
        using var scope = world.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppIdentityDbContext>();
        return await db.AccessVersions.AnyAsync(v => v.TenantId == tenantId, ct);
    }

    /// <summary>Fails the first access version write it sees, before the command reaches the database.</summary>
    private sealed class FailFirstAccessVersionWrite : Microsoft.EntityFrameworkCore.Diagnostics.DbCommandInterceptor
    {
        public bool Failed { get; private set; }

        public override ValueTask<Microsoft.EntityFrameworkCore.Diagnostics.InterceptionResult<int>> NonQueryExecutingAsync(
            System.Data.Common.DbCommand command,
            Microsoft.EntityFrameworkCore.Diagnostics.CommandEventData eventData,
            Microsoft.EntityFrameworkCore.Diagnostics.InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            if (!Failed && command.CommandText.Contains("AccessVersions", StringComparison.Ordinal))
            {
                Failed = true;
                throw new InvalidOperationException("Simulated access version write failure.");
            }

            return ValueTask.FromResult(result);
        }
    }

    /// <summary>Identity context options with the active provider's retrying execution strategy, as a host may configure.</summary>
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

    /// <summary>Identity context options for the active provider, with extra configuration.</summary>
    private DbContextOptions<AppIdentityDbContext> ProviderOptions(Action<DbContextOptionsBuilder<AppIdentityDbContext>> configure)
    {
        var builder = new DbContextOptionsBuilder<AppIdentityDbContext>();
        if (fixture.Database.Provider == TestDatabaseProvider.SqlServer)
        {
            builder.UseSqlServer(fixture.Database.ConnectionString);
        }
        else
        {
            builder.UseNpgsql(fixture.Database.ConnectionString);
        }

        configure(builder);
        return builder.Options;
    }

    private async Task<IntegrationTestWorld> PrepareAsync(CancellationToken ct)
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
    private static Task<RolePermission> TenantRolePermissionAsync(AppIdentityDbContext db, IntegrationTestWorld world, CancellationToken ct)
    {
        var tenantId = world.Tenants[0].Id;
        var roleId = db.Roles.Where(r => r.TenantId == tenantId && r.Name == ROLE_NAME).Select(r => r.Id);
        return db.RolePermissions.FirstAsync(rp => roleId.Contains(rp.RoleId), ct);
    }

    /// <summary>The versions a request in <paramref name="tenantId"/> keys on, read the way the cache reads them.</summary>
    private static async Task<AccessVersionKey> ReadVersionsAsync(IntegrationTestWorld world, long tenantId, CancellationToken ct)
    {
        using var scope = world.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<IAccessVersionStore>().ReadAsync(tenantId, ct);
    }

    /// <summary>The world tenant's version, read the way the cache reads it.</summary>
    private static async Task<Guid> TenantVersionAsync(IntegrationTestWorld world, CancellationToken ct) =>
        (await ReadVersionsAsync(world, world.Tenants[0].Id, ct)).Scope;
}
