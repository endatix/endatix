using Endatix.Core.Abstractions.Authorization;
using Endatix.Core.Entities.Identity;
using Endatix.Infrastructure.Identity;
using Endatix.Infrastructure.Identity.Authorization;
using Endatix.Infrastructure.Identity.Repositories;
using Endatix.IntegrationTests.Shared;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using static Endatix.IntegrationTests.AccessVersionTestWorld;

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

    [Fact]
    public async Task SaveChangesAsync_UserRoleAdded_ChangesTenantVersionOnly()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var world = await PrepareAsync(fixture, ct);
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
        var world = await PrepareAsync(fixture, ct);
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
        var world = await PrepareAsync(fixture, ct);
        var before = await AllRowsAsync(world, ct);

        // Act
        using (var scope = world.Services.CreateScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
            var user = (await users.FindByNameAsync(USER_NAME))!;
            user.PhoneNumber = "+10000000000";
            (await users.UpdateAsync(user)).Succeeded.Should().BeTrue();
        }

        // Assert
        (await AllRowsAsync(world, ct)).Should().Equal(before);
    }

    [Fact]
    public async Task SaveChangesAsync_NewUserGetsFirstRoleInSameTransaction_KeepsAccessVersions()
    {
        // Arrange — self-registration creates the user and grants the first role in one transaction; a user created
        // in it holds no token yet, so nothing cached can be stale.
        var ct = TestContext.Current.CancellationToken;
        var world = await PrepareAsync(fixture, ct);
        var before = await AllRowsAsync(world, ct);

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
        (await AllRowsAsync(world, ct)).Should().Equal(before);
    }

    [Fact]
    public async Task SaveChangesAsync_UserCreatedInEarlierSaveGetsRole_ChangesTenantVersion()
    {
        // Arrange — a long-lived context (a job, an import) creates a user in one save without a transaction.
        var ct = TestContext.Current.CancellationToken;
        var world = await PrepareAsync(fixture, ct);
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
        var world = await PrepareAsync(fixture, ct);
        var before = await AllRowsAsync(world, ct);
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
        (await AllRowsAsync(world, ct)).Should().Equal(before);
        (await db.RolePermissions.AsNoTracking().CountAsync(ct)).Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task DeleteRole_ThroughUnitOfWorkTransaction_ChangesTenantVersion()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var world = await PrepareAsync(fixture, ct);
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
        var world = await PrepareAsync(fixture, ct);
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
        var world = await PrepareAsync(fixture, ct);
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
        var world = await PrepareAsync(fixture, ct);
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
        var world = await PrepareAsync(fixture, ct);
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
        var world = await PrepareAsync(fixture, ct);
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
    public async Task Commit_ClearsUsersAddedInTheTransaction()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var world = await PrepareAsync(fixture, ct);
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
        var world = await PrepareAsync(fixture, ct);
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

    /// <summary>The world tenant's row, the system row and the row every change moves.</summary>
    private static Task<Dictionary<long, Guid>> AllRowsAsync(IntegrationTestWorld world, CancellationToken ct) =>
        RowsAsync(world, ct, world.Tenants[0].Id, SYSTEM_TENANT, ANY_TENANT);
}
