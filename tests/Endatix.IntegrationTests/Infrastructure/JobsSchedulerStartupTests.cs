using System.Data.Common;
using Endatix.IntegrationTests.Infrastructure.Jobs;
using Endatix.IntegrationTests.Shared;
using Endatix.Modules.Jobs.Runtime;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Npgsql;
using Quartz;

namespace Endatix.IntegrationTests;

/// <summary>
/// How a jobs node starts when its database is not there: the host comes up and the scheduler waits for the database,
/// on either provider.
/// </summary>
[Collection(nameof(DbIntegrationTestCollection))]
[Trait("Category", "Infrastructure")]
[Trait("Priority", "P1")]
public sealed class JobsSchedulerStartupTests(DbIntegrationFixture fixture)
{
    private const string DeferredStartError =
        "The background jobs scheduler could not reach its database and will start once it can; until then this host runs no jobs.";

    [Fact]
    public async Task Scheduler_with_unreachable_database_defers_start()
    {
        // Arrange — nothing listens on port 1, and the short timeout keeps each try well inside the wait below.
        var ct = TestContext.Current.CancellationToken;
        var logs = new CapturingLoggerProvider();
        await using var node = JobsTestNode.Create(
            ClosedPort(fixture.Provider),
            configureServices: services => services.AddSingleton<ILoggerProvider>(logs),
            migrate: false);

        // Act
        await node.StartAsync(ct);
        await Task.Delay(TimeSpan.FromSeconds(10), ct);

        // Assert
        logs.Entries.Should().Contain((LogLevel.Error, DeferredStartError));
        logs.Entries.Should().NotContain(entry => entry.Level == LogLevel.Critical);
    }

    [Fact]
    public async Task Scheduler_whose_login_is_refused_fails_startup()
    {
        // Arrange — the server is there and says no, which waiting would never change.
        var ct = TestContext.Current.CancellationToken;
        var logs = new CapturingLoggerProvider();
        await using var node = JobsTestNode.Create(
            WrongPassword(fixture.Provider, fixture.ConnectionString),
            configureServices: services => services.AddSingleton<ILoggerProvider>(logs),
            migrate: false);

        // Act
        var start = () => node.StartAsync(ct);

        // Assert
        await start.Should().ThrowAsync<DbException>();
        logs.Entries.Should().NotContain((LogLevel.Error, DeferredStartError));
    }

    [Fact]
    [Trait("DbSpecific", "SqlServer")]
    public async Task Scheduler_whose_database_cannot_be_opened_defers_start_and_names_the_database()
    {
        // Arrange — SQL Server-only: it answers a missing database with 4060 and 18456, as it answers one being
        // restored, so the node waits and says which database it cannot open.
        Assert.SkipWhen(fixture.Provider != TestDatabaseProvider.SqlServer, "Asserts SQL Server's 4060 and 18456.");
        var ct = TestContext.Current.CancellationToken;
        var missing = new SqlConnectionStringBuilder(fixture.ConnectionString)
        {
            InitialCatalog = $"t9_missing_{Guid.NewGuid():N}"[..24],
        };
        var logs = new CapturingLoggerProvider();
        await using var node = JobsTestNode.Create(
            new JobsTestNodeDatabase(TestDatabaseProvider.SqlServer, missing.ConnectionString),
            configureServices: services => services.AddSingleton<ILoggerProvider>(logs),
            migrate: false);

        // Act
        await node.StartAsync(ct);

        // Assert
        logs.Entries.Should().Contain((LogLevel.Error, DeferredStartError));
        logs.Entries.Should().Contain((
            LogLevel.Error,
            $"The background jobs scheduler cannot open database '{missing.InitialCatalog}': it is missing, offline or being restored. Retrying."));
        logs.Entries.Should().NotContain(entry => entry.Message.Contains("Password", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Scheduler_that_loses_a_race_while_starting_tries_again_without_reporting_an_outage()
    {
        // Arrange — the scheduler cannot be built once because another node's transaction won a deadlock.
        var ct = TestContext.Current.CancellationToken;
        var scheduler = Substitute.For<IScheduler>();
        var schedulers = Substitute.For<ISchedulerFactory>();
        schedulers.GetScheduler(Arg.Any<CancellationToken>()).Returns(
            _ => throw new SchedulerException(
                "The store failed.",
                new PostgresException("deadlock detected", "ERROR", "ERROR", PostgresErrorCodes.DeadlockDetected)),
            _ => ValueTask.FromResult(scheduler));
        var logs = new CapturingLoggerProvider();
        using var service = HostedServiceOver(fixture, schedulers, logs);

        // Act
        await service.StartAsync(ct);
        var started = await JobsTestWait.UntilAsync(
            () => Task.FromResult(scheduler.ReceivedCalls().Any(call => call.GetMethodInfo().Name == nameof(IScheduler.Start))),
            TimeSpan.FromSeconds(20),
            ct);
        await service.StopAsync(ct);

        // Assert
        started.Should().BeTrue();
        logs.Entries.Should().Contain(entry => entry.Level == LogLevel.Warning && entry.Message.Contains("lost a race"));
        logs.Entries.Should().NotContain((LogLevel.Error, DeferredStartError));
        logs.Entries.Should().NotContain(entry => entry.Level == LogLevel.Critical);
    }

    [Fact]
    public async Task Scheduler_whose_database_goes_away_after_the_probe_waits_and_starts()
    {
        // Arrange — the database answers the probe, then the scheduler cannot be built once, as in a failover.
        var ct = TestContext.Current.CancellationToken;
        var scheduler = Substitute.For<IScheduler>();
        var schedulers = Substitute.For<ISchedulerFactory>();
        schedulers.GetScheduler(Arg.Any<CancellationToken>()).Returns(
            _ => throw new SchedulerException("The store failed.", new TimeoutException("The database did not answer.")),
            _ => ValueTask.FromResult(scheduler));
        var logs = new CapturingLoggerProvider();
        using var service = HostedServiceOver(fixture, schedulers, logs);

        // Act
        await service.StartAsync(ct);
        var started = await JobsTestWait.UntilAsync(
            () => Task.FromResult(scheduler.ReceivedCalls().Any(call => call.GetMethodInfo().Name == nameof(IScheduler.Start))),
            TimeSpan.FromSeconds(20),
            ct);
        await service.StopAsync(ct);

        // Assert
        started.Should().BeTrue("the start waits out an outage at any step, not only at the probe");
        logs.Entries.Should().Contain((LogLevel.Error, DeferredStartError));
        logs.Entries.Should().NotContain(entry => entry.Level == LogLevel.Critical);
    }

    // The hosted service alone, over the fixture's database, with the scheduler factory a test controls.
    private static JobsSchedulerHostedService HostedServiceOver(
        DbIntegrationFixture fixture,
        ISchedulerFactory schedulers,
        CapturingLoggerProvider logs)
    {
        var services = new ServiceCollection()
            .AddKeyedSingleton(QuartzRegistration.SchedulerName, schedulers)
            .BuildServiceProvider();
        var database = new JobStoreDatabase(fixture.ConnectionString, fixture.Provider == TestDatabaseProvider.PostgreSql);
        var options = Options.Create(new BackgroundJobsOptions());
        return new JobsSchedulerHostedService(
            services,
            database,
            JobHandlerRegistry.Build([]),
            options,
            new JobsShutdownSignal(),
            new LoggerFactory([logs]).CreateLogger<JobsSchedulerHostedService>());
    }

    private static JobsTestNodeDatabase WrongPassword(TestDatabaseProvider provider, string connectionString) =>
        provider switch
        {
            TestDatabaseProvider.PostgreSql => new(
                provider, new NpgsqlConnectionStringBuilder(connectionString) { Password = "wrong" }.ConnectionString),
            TestDatabaseProvider.SqlServer => new(
                provider, new SqlConnectionStringBuilder(connectionString) { Password = "wrong" }.ConnectionString),
            _ => throw new ArgumentOutOfRangeException(nameof(provider), provider, "Unsupported test database provider."),
        };

    private static JobsTestNodeDatabase ClosedPort(TestDatabaseProvider provider) => provider switch
    {
        TestDatabaseProvider.PostgreSql => new(
            provider, "Host=127.0.0.1;Port=1;Database=endatix;Username=endatix;Password=endatix;Timeout=1"),
        TestDatabaseProvider.SqlServer => new(
            provider,
            "Server=127.0.0.1,1;Database=endatix;User ID=endatix;Password=endatix;Connect Timeout=1;TrustServerCertificate=True"),
        _ => throw new ArgumentOutOfRangeException(nameof(provider), provider, "Unsupported test database provider."),
    };
}
