using System.Data.Common;
using Endatix.IntegrationTests.Infrastructure.Jobs;
using Endatix.Framework.Modules;
using Endatix.IntegrationTests.Shared;
using Endatix.Modules.Jobs;
using Endatix.Modules.Jobs.Runtime;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Quartz;
using Quartz.Impl.AdoJobStore;

namespace Endatix.IntegrationTests;

/// <summary>
/// The dialect the trigger-store failure tests put on a node replaces the one the module registers, and keeps its
/// acquisition, so those tests schedule as production does. The services come from the module's own registration, so a
/// change in how or under which key it registers its dialect fails here. No database is involved and no scheduler
/// starts: acquisition with every group full returns before any statement runs, which only the module's override does.
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
        var acquired = await AcquireWithEveryGroupFullAsync(registered.Failing, connection);

        // Assert
        registered.ModuleDelegate.Should().Be<ExecutionGroupFilteringPostgreSqlDelegate>();
        registered.Failing.GetType().BaseType.Should().Be<ExecutionGroupFilteringPostgreSqlDelegate>();
        acquired.Should().BeEmpty();
    }

    [Fact]
    public async Task Register_SqlServer_KeepsTheModulesGroupAcquisition()
    {
        // Arrange
        var registered = RegisteredFailingDelegate(TestDatabaseProvider.SqlServer);
        await using var connection = new SqlConnection();

        // Act
        var acquired = await AcquireWithEveryGroupFullAsync(registered.Failing, connection);

        // Assert
        registered.ModuleDelegate.Should().Be<ExecutionGroupFilteringSqlServerDelegate>();
        registered.Failing.GetType().BaseType.Should().Be<ExecutionGroupFilteringSqlServerDelegate>();
        acquired.Should().BeEmpty();
    }

    [Theory]
    [InlineData(TestDatabaseProvider.PostgreSql)]
    [InlineData(TestDatabaseProvider.SqlServer)]
    public async Task Register_RebuildingTheScheduler_IsRefused(TestDatabaseProvider provider)
    {
        // Arrange — a rebuild replays only the scheduler's recipe, which would drop the failing dialect.
        var services = ModuleServices(provider);
        FailingTriggerStoreDelegate.Register(services, provider, TriggerStoreFailures.Rejected(stores: 1));
        await using var withFailures = services.BuildServiceProvider();

        // Act
        var rebuild = () => withFailures.GetRequiredService<ISchedulerRuntime>()
            .Restart(QuartzRegistration.SchedulerName, SchedulerRestartOptions.WithoutStarting).AsTask();

        // Assert
        await rebuild.Should().ThrowAsync<InvalidOperationException>().WithMessage("*would drop the failing dialect*");
    }

    // The dialect the module registers for its scheduler, and the one a node gets once the failing one replaces it.
    private static RegisteredDelegates RegisteredFailingDelegate(TestDatabaseProvider provider)
    {
        var services = ModuleServices(provider);
        Type moduleDelegate;
        using (var moduleOnly = services.BuildServiceProvider())
        {
            moduleDelegate = SchedulerDelegate(moduleOnly).GetType();
        }

        FailingTriggerStoreDelegate.Register(services, provider, TriggerStoreFailures.Rejected(stores: 1));
        using var withFailures = services.BuildServiceProvider();
        return new RegisteredDelegates(moduleDelegate, SchedulerDelegate(withFailures));
    }

    // The module's whole registration, as a node with the module on gets it; nothing here connects or starts.
    private static ServiceCollection ModuleServices(TestDatabaseProvider provider)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = provider == TestDatabaseProvider.PostgreSql
                    ? "Host=127.0.0.1;Database=not_connected;Username=x;Password=x"
                    : "Server=127.0.0.1;Database=not_connected;User Id=x;Password=x",
                ["ConnectionStrings:DefaultConnection_DbProvider"] = provider.ToString(),
            })
            .Build();
        var services = new ServiceCollection();
        services.AddLogging();
        JobsModule.Instance.ConfigureServices(new EndatixModuleBuilder(services, configuration));
        return services;
    }

    private static IDriverDelegate SchedulerDelegate(IServiceProvider services) =>
        services.GetRequiredKeyedService<IDriverDelegate>(QuartzRegistration.SchedulerName);

    /// <param name="ModuleDelegate">The dialect type the module registered.</param>
    /// <param name="Failing">The dialect registered in its place.</param>
    private sealed record RegisteredDelegates(Type ModuleDelegate, IDriverDelegate Failing);

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
