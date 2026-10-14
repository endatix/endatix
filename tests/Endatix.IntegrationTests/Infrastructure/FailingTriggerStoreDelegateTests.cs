using System.Data.Common;
using Endatix.IntegrationTests.Infrastructure.Jobs;
using Endatix.IntegrationTests.Shared;
using Endatix.Modules.Jobs.Runtime;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Quartz;
using Quartz.Impl.AdoJobStore;

namespace Endatix.IntegrationTests;

/// <summary>
/// The dialect the trigger-store failure tests put on a node keeps the module's acquisition, so those tests schedule
/// as production does. No database is involved: acquisition with every group full returns before any statement runs,
/// which only the module's override does.
/// </summary>
[Trait("Category", "Infrastructure")]
[Trait("Priority", "P1")]
public sealed class FailingTriggerStoreDelegateTests
{
    [Fact]
    public async Task Register_PostgreSql_KeepsTheModulesGroupAcquisition()
    {
        // Arrange
        var registered = RegisteredFailingDelegate(TestDatabaseProvider.PostgreSql);
        await using var connection = new NpgsqlConnection();

        // Act
        var acquired = await AcquireWithEveryGroupFullAsync(registered, connection);

        // Assert
        registered.Should().BeAssignableTo<ExecutionGroupFilteringPostgreSqlDelegate>();
        acquired.Should().BeEmpty();
    }

    [Fact]
    public async Task Register_SqlServer_KeepsTheModulesGroupAcquisition()
    {
        // Arrange
        var registered = RegisteredFailingDelegate(TestDatabaseProvider.SqlServer);
        await using var connection = new SqlConnection();

        // Act
        var acquired = await AcquireWithEveryGroupFullAsync(registered, connection);

        // Assert
        registered.Should().BeAssignableTo<ExecutionGroupFilteringSqlServerDelegate>();
        acquired.Should().BeEmpty();
    }

    // The dialect a node gets once the failing one replaces what the module registered.
    private static IDriverDelegate RegisteredFailingDelegate(TestDatabaseProvider provider)
    {
        var services = new ServiceCollection();
        services.AddKeyedSingleton<IDriverDelegate>(QuartzRegistration.SchedulerName, new StdAdoDelegate());
        FailingTriggerStoreDelegate.Register(services, provider, TriggerStoreFailures.Rejected(stores: 1));
        using var provided = services.BuildServiceProvider();
        return provided.GetRequiredKeyedService<IDriverDelegate>(QuartzRegistration.SchedulerName);
    }

    // The connection is never opened, so Quartz's own acquisition, which always queries, would throw.
    private static async Task<List<TriggerAcquireResult>> AcquireWithEveryGroupFullAsync(
        IDriverDelegate driverDelegate,
        DbConnection connection)
    {
        var now = DateTimeOffset.UtcNow;
        var criteria = new TriggerAcquisitionCriteria
        {
            NoLaterThan = now,
            NoEarlierThan = now.AddMinutes(-1),
            LiveNodeCutoff = now.AddSeconds(-15),
            MaxCount = 2,
            ExecutionLimits = ExecutionLimitsBuilder.Create().ForGroup(ProbePayload.JobType, 0).ForOtherGroups(0).Build(),
        };
        return await driverDelegate.SelectTriggersToAcquire(
            new ConnectionAndTransactionHolder(connection, null), criteria, TestContext.Current.CancellationToken);
    }
}
