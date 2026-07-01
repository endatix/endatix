using Endatix.Infrastructure.Data;
using Endatix.Infrastructure.Identity;
using Endatix.IntegrationTests.Shared;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Endatix.IntegrationTests;

[Collection(nameof(DbIntegrationTestCollection))]
[Trait("Category", "Infrastructure")]
[Trait("Priority", "P0")]
public sealed class StartupMigrationTests
{
    private readonly DbIntegrationFixture _fixture;

    public StartupMigrationTests(DbIntegrationFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task ApplyDbMigrations_on_empty_db_creates_core_schemas()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        await _fixture.Checkpoint.ResetAsync(_fixture.ConnectionString, _fixture.Provider, cancellationToken);

        var provider = IntegrationCoreMigrationTestHelper.BuildServiceProvider(
            _fixture.ConnectionString,
            _fixture.Provider);

        // Act
        await provider.ApplyDbMigrationsAsync(cancellationToken);

        // Assert
        var usersTableExists = await IntegrationDbAssert.TableExistsAsync(
            _fixture.ConnectionString,
            _fixture.Provider,
            schema: "identity",
            table: "Users",
            cancellationToken);
        Assert.True(usersTableExists);

        var formsTableExists = await IntegrationDbAssert.TableExistsAsync(
            _fixture.ConnectionString,
            _fixture.Provider,
            schema: _fixture.Provider == TestDatabaseProvider.PostgreSql ? "public" : "dbo",
            table: "Forms",
            cancellationToken);
        Assert.True(formsTableExists);

        using var scope = provider.CreateScope();
        var appDb = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var identityDb = scope.ServiceProvider.GetRequiredService<AppIdentityDbContext>();

        var appHistory = (await appDb.Database.GetAppliedMigrationsAsync(cancellationToken)).ToList();
        var identityHistory = (await identityDb.Database.GetAppliedMigrationsAsync(cancellationToken)).ToList();

        Assert.NotEmpty(appHistory);
        Assert.NotEmpty(identityHistory);
    }

    [Fact]
    public async Task ApplyDbMigrations_is_idempotent()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        await _fixture.Checkpoint.ResetAsync(_fixture.ConnectionString, _fixture.Provider, cancellationToken);

        var provider = IntegrationCoreMigrationTestHelper.BuildServiceProvider(
            _fixture.ConnectionString,
            _fixture.Provider);

        await provider.ApplyDbMigrationsAsync(cancellationToken);

        using (var scope = provider.CreateScope())
        {
            var appDb = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var identityDb = scope.ServiceProvider.GetRequiredService<AppIdentityDbContext>();

            var appHistoryAfterFirst = (await appDb.Database.GetAppliedMigrationsAsync(cancellationToken)).ToList();
            var identityHistoryAfterFirst = (await identityDb.Database.GetAppliedMigrationsAsync(cancellationToken)).ToList();

            // Act
            await provider.ApplyDbMigrationsAsync(cancellationToken);

            // Assert
            using var scopeAfter = provider.CreateScope();
            var appDbAfter = scopeAfter.ServiceProvider.GetRequiredService<AppDbContext>();
            var identityDbAfter = scopeAfter.ServiceProvider.GetRequiredService<AppIdentityDbContext>();

            var appHistoryAfterSecond = (await appDbAfter.Database.GetAppliedMigrationsAsync(cancellationToken)).ToList();
            var identityHistoryAfterSecond = (await identityDbAfter.Database.GetAppliedMigrationsAsync(cancellationToken)).ToList();

            Assert.Equal(appHistoryAfterFirst, appHistoryAfterSecond);
            Assert.Equal(identityHistoryAfterFirst, identityHistoryAfterSecond);
        }
    }

    [Fact]
    public async Task ApplyDbMigrations_applies_latest_pending_migrations()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        await _fixture.Checkpoint.ResetAsync(_fixture.ConnectionString, _fixture.Provider, cancellationToken);

        var provider = IntegrationCoreMigrationTestHelper.BuildServiceProvider(
            _fixture.ConnectionString,
            _fixture.Provider);

        // Act
        await provider.ApplyDbMigrationsAsync(cancellationToken);

        // Assert
        using var scope = provider.CreateScope();
        var appDb = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var identityDb = scope.ServiceProvider.GetRequiredService<AppIdentityDbContext>();

        var latestAppMigration = appDb.Database.GetMigrations().Last();
        var latestIdentityMigration = identityDb.Database.GetMigrations().Last();

        var appliedAppMigrations = (await appDb.Database.GetAppliedMigrationsAsync(cancellationToken)).ToList();
        var appliedIdentityMigrations = (await identityDb.Database.GetAppliedMigrationsAsync(cancellationToken)).ToList();

        Assert.Contains(latestAppMigration, appliedAppMigrations);
        Assert.Contains(latestIdentityMigration, appliedIdentityMigrations);
    }
}
