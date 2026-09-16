using Endatix.IntegrationTests.Shared;

namespace Endatix.IntegrationTests;

/// <summary>
/// The jobs schema as the startup migrations leave it. Unit tests pin what the entity exposes; only a
/// migrated database can show that the table agrees with it.
/// </summary>
[Collection(nameof(EndatixIntegrationTestCollection))]
[Trait("Category", "Infrastructure")]
[Trait("Priority", "P1")]
[Trait("DbSpecific", "PostgreSql")]
public sealed class JobsSchemaIntegrationTests(EndatixIntegrationWebHostFixture fixture)
{
    [Fact]
    public async Task BackgroundJobsTable_AfterMigrations_HasNoResultJsonColumn()
    {
        // Arrange — the module is PostgreSQL-only, so the flag is off on a SQL Server run.
        Assert.SkipWhen(
            fixture.Provider != TestDatabaseProvider.PostgreSql,
            "Background jobs are PostgreSQL-only; the module is not registered on this provider.");

        var cancellationToken = TestContext.Current.CancellationToken;
        using var client = fixture.CreateClient();
        _ = await client.GetAsync(new Uri("/health", UriKind.Relative), cancellationToken);

        // Act
        var resultColumnExists = await IntegrationDbAssert.SqlRowExistsAsync(
            fixture.Database.ConnectionString,
            fixture.Database.Provider,
            """
            SELECT EXISTS (
                SELECT 1
                FROM information_schema.columns
                WHERE table_schema = 'jobs'
                  AND table_name = 'BackgroundJobs'
                  AND column_name = 'ResultJson')
            """,
            cancellationToken);

        var payloadColumnIsJsonb = await IntegrationDbAssert.SqlRowExistsAsync(
            fixture.Database.ConnectionString,
            fixture.Database.Provider,
            """
            SELECT (
                SELECT count(*)
                FROM information_schema.columns
                WHERE table_schema = 'jobs'
                  AND table_name = 'BackgroundJobs'
                  AND column_name = 'PayloadJson'
                  AND data_type = 'jsonb') = 1
            """,
            cancellationToken);

        // Assert — the row carries state only. Handler input stays, because the handler is given it.
        resultColumnExists.Should().BeFalse();
        payloadColumnIsJsonb.Should().BeTrue();
    }
}
