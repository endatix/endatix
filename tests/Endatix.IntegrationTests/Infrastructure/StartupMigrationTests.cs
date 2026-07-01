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
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        await _fixture.Checkpoint.ResetAsync(_fixture.ConnectionString, _fixture.Provider, cancellationToken);

        IServiceProvider provider = IntegrationCoreMigrationTestHelper.BuildServiceProvider(
            _fixture.ConnectionString,
            _fixture.Provider);

        // Act
        await provider.ApplyDbMigrationsAsync(cancellationToken);

        // Assert
        bool usersTableExists = await IntegrationCoreMigrationTestHelper.TableExistsAsync(
            _fixture.ConnectionString,
            _fixture.Provider,
            schema: "identity",
            table: "Users",
            cancellationToken);
        Assert.True(usersTableExists);

        bool formsTableExists = await IntegrationCoreMigrationTestHelper.TableExistsAsync(
            _fixture.ConnectionString,
            _fixture.Provider,
            schema: _fixture.Provider == TestDatabaseProvider.PostgreSql ? "public" : "dbo",
            table: "Forms",
            cancellationToken);
        Assert.True(formsTableExists);

        using IServiceScope scope = provider.CreateScope();
        AppDbContext appDb = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        AppIdentityDbContext identityDb = scope.ServiceProvider.GetRequiredService<AppIdentityDbContext>();

        List<string> appHistory = (await appDb.Database.GetAppliedMigrationsAsync(cancellationToken)).ToList();
        List<string> identityHistory = (await identityDb.Database.GetAppliedMigrationsAsync(cancellationToken)).ToList();

        Assert.NotEmpty(appHistory);
        Assert.NotEmpty(identityHistory);
    }

    [Fact]
    public async Task ApplyDbMigrations_is_idempotent()
    {
        // Arrange
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        await _fixture.Checkpoint.ResetAsync(_fixture.ConnectionString, _fixture.Provider, cancellationToken);

        IServiceProvider provider = IntegrationCoreMigrationTestHelper.BuildServiceProvider(
            _fixture.ConnectionString,
            _fixture.Provider);

        await provider.ApplyDbMigrationsAsync(cancellationToken);

        using (IServiceScope scope = provider.CreateScope())
        {
            AppDbContext appDb = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            AppIdentityDbContext identityDb = scope.ServiceProvider.GetRequiredService<AppIdentityDbContext>();

            List<string> appHistoryAfterFirst = (await appDb.Database.GetAppliedMigrationsAsync(cancellationToken)).ToList();
            List<string> identityHistoryAfterFirst = (await identityDb.Database.GetAppliedMigrationsAsync(cancellationToken)).ToList();

            // Act
            await provider.ApplyDbMigrationsAsync(cancellationToken);

            // Assert
            using IServiceScope scopeAfter = provider.CreateScope();
            AppDbContext appDbAfter = scopeAfter.ServiceProvider.GetRequiredService<AppDbContext>();
            AppIdentityDbContext identityDbAfter = scopeAfter.ServiceProvider.GetRequiredService<AppIdentityDbContext>();

            List<string> appHistoryAfterSecond = (await appDbAfter.Database.GetAppliedMigrationsAsync(cancellationToken)).ToList();
            List<string> identityHistoryAfterSecond = (await identityDbAfter.Database.GetAppliedMigrationsAsync(cancellationToken)).ToList();

            Assert.Equal(appHistoryAfterFirst, appHistoryAfterSecond);
            Assert.Equal(identityHistoryAfterFirst, identityHistoryAfterSecond);
        }
    }

    [Fact]
    public async Task ApplyDbMigrations_applies_latest_pending_migrations()
    {
        // Arrange
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        await _fixture.Checkpoint.ResetAsync(_fixture.ConnectionString, _fixture.Provider, cancellationToken);

        IServiceProvider provider = IntegrationCoreMigrationTestHelper.BuildServiceProvider(
            _fixture.ConnectionString,
            _fixture.Provider);

        // Act
        await provider.ApplyDbMigrationsAsync(cancellationToken);

        // Assert
        using IServiceScope scope = provider.CreateScope();
        AppDbContext appDb = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        AppIdentityDbContext identityDb = scope.ServiceProvider.GetRequiredService<AppIdentityDbContext>();

        string latestAppMigration = appDb.Database.GetMigrations().Last();
        string latestIdentityMigration = identityDb.Database.GetMigrations().Last();

        List<string> appliedAppMigrations = (await appDb.Database.GetAppliedMigrationsAsync(cancellationToken)).ToList();
        List<string> appliedIdentityMigrations = (await identityDb.Database.GetAppliedMigrationsAsync(cancellationToken)).ToList();

        Assert.Contains(latestAppMigration, appliedAppMigrations);
        Assert.Contains(latestIdentityMigration, appliedIdentityMigrations);
    }
}
