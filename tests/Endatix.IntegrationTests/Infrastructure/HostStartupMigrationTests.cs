using Endatix.Infrastructure.Data;
using Endatix.Infrastructure.Identity;
using Endatix.IntegrationTests.Shared;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Endatix.IntegrationTests;

[Collection(nameof(EndatixIntegrationTestCollection))]
[Trait("Category", "Infrastructure")]
[Trait("Priority", "P0")]
public sealed class HostStartupMigrationTests
{
    private readonly EndatixIntegrationWebHostFixture _fixture;

    public HostStartupMigrationTests(EndatixIntegrationWebHostFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Host_startup_migrates_core_on_empty_db()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        await _fixture.ResetDatabaseAsync(cancellationToken: cancellationToken);

        await using EndatixWebApplicationFactory factory = new(
            _fixture.Database.ConnectionString,
            _fixture.Database.Provider);

        // Act
        using var client = factory.CreateClient();
        _ = await client.GetAsync(new Uri("/health", UriKind.Relative), cancellationToken);

        // Assert
        using var scope = factory.Services.CreateScope();
        var appDb = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var identityDb = scope.ServiceProvider.GetRequiredService<AppIdentityDbContext>();

        var appliedAppMigrations = (await appDb.Database.GetAppliedMigrationsAsync(cancellationToken)).ToList();
        var appliedIdentityMigrations = (await identityDb.Database.GetAppliedMigrationsAsync(cancellationToken)).ToList();

        Assert.NotEmpty(appliedAppMigrations);
        Assert.NotEmpty(appliedIdentityMigrations);

        var usersTableExists = await IntegrationDbAssert.TableExistsAsync(
            _fixture.Database.ConnectionString,
            _fixture.Database.Provider,
            schema: "identity",
            table: "Users",
            cancellationToken);
        Assert.True(usersTableExists);

        Assert.Contains(
            appliedAppMigrations,
            name => name.Contains("SubmissionStartedAt", StringComparison.Ordinal));

        bool startedAtColumnExists = await IntegrationDbAssert.SqlRowExistsAsync(
            _fixture.Database.ConnectionString,
            _fixture.Database.Provider,
            _fixture.Database.Provider == TestDatabaseProvider.PostgreSql
                ? """
                  SELECT EXISTS (
                      SELECT 1
                      FROM information_schema.columns
                      WHERE table_schema = 'public'
                        AND table_name = 'Submissions'
                        AND column_name = 'StartedAt')
                  """
                : """
                  SELECT CASE WHEN EXISTS (
                      SELECT 1
                      FROM INFORMATION_SCHEMA.COLUMNS
                      WHERE TABLE_SCHEMA = 'dbo'
                        AND TABLE_NAME = 'Submissions'
                        AND COLUMN_NAME = 'StartedAt')
                  THEN 1 ELSE 0 END
                  """,
            cancellationToken);
        Assert.True(startedAtColumnExists);

        string exportSchema = _fixture.Database.Provider == TestDatabaseProvider.PostgreSql
            ? "public"
            : "dbo";
        Assert.True(await IntegrationDbAssert.RoutineExistsAsync(
            _fixture.Database.ConnectionString,
            _fixture.Database.Provider,
            schema: exportSchema,
            routineName: "export_form_submissions",
            cancellationToken));
    }
}
