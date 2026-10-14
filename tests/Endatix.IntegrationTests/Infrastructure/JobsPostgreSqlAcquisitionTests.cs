using Endatix.IntegrationTests.Infrastructure.Jobs;
using Endatix.IntegrationTests.Shared;
using Endatix.Modules.Jobs.Runtime;

namespace Endatix.IntegrationTests;

/// <summary>
/// Which triggers the PostgreSQL driver delegate acquires, on the fixture the SQL Server delegate is proven on in
/// <see cref="JobsSqlServerStoreTests"/>, so both providers are held to the same keys.
/// </summary>
[Collection(nameof(DbIntegrationTestCollection))]
[Trait("Category", "Infrastructure")]
[Trait("Priority", "P1")]
[Trait("DbSpecific", "PostgreSql")]
public sealed class JobsPostgreSqlAcquisitionTests(DbIntegrationFixture fixture)
{
    [Fact]
    public async Task SelectTriggersToAcquire_FullGroupAtHeadOfQueue_ReturnsOtherGroupsTrigger()
    {
        // Arrange
        Assert.SkipWhen(fixture.Provider != TestDatabaseProvider.PostgreSql, JobsSkipReason.PostgreSqlSpecificSql);
        var ct = TestContext.Current.CancellationToken;
        await using var store = await AcquisitionTestStore.CreateAsync(TestDatabaseProvider.PostgreSql, fixture.ConnectionString, ct);
        var behind = await store.SeedHeadOfLineAsync(ct);

        // Act
        var acquired = await store.SelectTriggersToAcquireAsync(2, limits => limits.ForGroup("A", 0).ForGroup("B", 1), ct);

        // Assert
        store.DriverDelegate.Should().BeOfType<ExecutionGroupFilteringPostgreSqlDelegate>();
        acquired.Should().Equal(behind);
    }
}
