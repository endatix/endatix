using Endatix.Infrastructure.Data;
using Endatix.Infrastructure.Identity;
using Endatix.IntegrationTests.Shared;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace Endatix.IntegrationTests;

/// <summary>
/// SQL Server–only migration artifacts (stored proc, identity seed). Run with <c>ENDATIX_TEST_DB_PROVIDER=SqlServer</c>.
/// </summary>
[Collection(nameof(DbIntegrationTestCollection))]
[Trait("Category", "Infrastructure")]
[Trait("Priority", "P0")]
[Trait("DbSpecific", "SqlServer")]
public sealed class SqlServerMigrationArtifactTests
{
    private readonly DbIntegrationFixture _fixture;

    public SqlServerMigrationArtifactTests(DbIntegrationFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task ApplyDbMigrations_creates_sql_server_specific_artifacts()
    {
        var cancellationToken = TestContext.Current.CancellationToken;

        var provider = IntegrationCoreMigrationTestHelper.BuildServiceProvider(
            _fixture.ConnectionString,
            _fixture.Provider);

        await provider.ApplyDbMigrationsAsync(NullLogger.Instance, cancellationToken);

        using var scope = provider.CreateScope();
        var appDb = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var identityDb = scope.ServiceProvider.GetRequiredService<AppIdentityDbContext>();

        var appliedAppMigrations = (await appDb.Database.GetAppliedMigrationsAsync(cancellationToken)).ToList();
        var appliedIdentityMigrations = (await identityDb.Database.GetAppliedMigrationsAsync(cancellationToken)).ToList();

        Assert.Contains(appDb.Database.GetMigrations().Last(), appliedAppMigrations);
        Assert.Contains(identityDb.Database.GetMigrations().Last(), appliedIdentityMigrations);
        Assert.Contains("20260701124643_SeedRespondentRole", appliedIdentityMigrations);

        Assert.True(await IntegrationDbAssert.TableExistsAsync(
            _fixture.ConnectionString, _fixture.Provider, schema: "dbo", table: "Submitters", cancellationToken));
        Assert.True(await IntegrationDbAssert.TableExistsAsync(
            _fixture.ConnectionString, _fixture.Provider, schema: "dbo", table: "OutboxMessages", cancellationToken));
        Assert.True(await IntegrationDbAssert.RoutineExistsAsync(
            _fixture.ConnectionString, _fixture.Provider, schema: "dbo", routineName: "export_form_submissions", cancellationToken));
    }
}
