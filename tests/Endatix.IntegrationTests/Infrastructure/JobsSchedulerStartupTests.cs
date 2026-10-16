using System.Data.Common;
using System.Net.Sockets;
using Endatix.IntegrationTests.Infrastructure.Jobs;
using Endatix.IntegrationTests.Shared;
using Endatix.Modules.Jobs.Runtime;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
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
public sealed partial class JobsSchedulerStartupTests(DbIntegrationFixture fixture)
{
    // How the error a deferred start logs begins; the count of tries and the driver's failure follow.
    private const string DeferredStartError =
        "The background jobs scheduler could not reach its database and will start once it can; until then this host runs no jobs";

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
        LoggedError(logs, DeferredStartError).Should().BeTrue();
        logs.Entries.Should().NotContain(entry => entry.Level == LogLevel.Critical);
    }

    [Fact]
    public async Task Scheduler_whose_host_does_not_resolve_logs_the_driver_failure_without_the_password()
    {
        // Arrange — a mistyped host is waited out as an outage is, so only the driver's failure in the log tells the
        // operator which of the two it is.
        var ct = TestContext.Current.CancellationToken;
        const string password = "Never-Logged-Pa55word";
        var logs = new CapturingLoggerProvider();
        await using var node = JobsTestNode.Create(
            UnresolvableHost(fixture.Provider, password),
            configureServices: services => services.AddSingleton<ILoggerProvider>(logs),
            migrate: false);

        // Act
        await node.StartAsync(ct);

        // Assert
        logs.Entries.Should().Contain(entry => entry.Level == LogLevel.Error
            && entry.Message.StartsWith(DeferredStartError, StringComparison.Ordinal)
            && entry.Message.Contains(nameof(SocketException)));
        logs.Entries.Should().NotContain(entry => entry.Message.Contains(password));
    }

    [Fact]
    public async Task Scheduler_warns_when_LockOnInsert_set_in_code_differs_from_the_one_in_effect()
    {
        // Arrange — configuration leaves the provider's default; the host sets the opposite in code, which the store
        // never sees.
        var ct = TestContext.Current.CancellationToken;
        await using var database = await JobsTestDatabase.CreateAsync(fixture, ct);
        var inEffect = fixture.Provider == TestDatabaseProvider.SqlServer;
        var logs = new CapturingLoggerProvider();
        await using var node = JobsTestNode.Create(
            database,
            configureServices: services =>
            {
                services.AddSingleton<ILoggerProvider>(logs);
                services.PostConfigure<BackgroundJobsOptions>(options => options.LockOnInsert = !inEffect);
            });

        // Act
        await node.StartAsync(ct);

        // Assert
        logs.Entries.Should().Contain(entry => entry.Level == LogLevel.Warning
            && entry.Message.Contains($"is {!inEffect} in the options but {inEffect} in the scheduler's store")
            && entry.Message.Contains("Configuration wins"));
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
        LoggedError(logs, DeferredStartError).Should().BeFalse();
    }

    [Fact]
    public async Task Scheduler_whose_database_cannot_be_opened_defers_start_and_names_the_database()
    {
        // Arrange — a database that does not exist yet, as while a pipeline still creates or restores it. SQL Server
        // answers 4060 with 18456 and PostgreSQL 3D000; both are waited out, naming the database.
        var ct = TestContext.Current.CancellationToken;
        var missingName = $"t9_missing_{Guid.NewGuid():N}"[..24];
        var logs = new CapturingLoggerProvider();
        await using var node = JobsTestNode.Create(
            MissingDatabase(fixture.Provider, fixture.ConnectionString, missingName),
            configureServices: services => services.AddSingleton<ILoggerProvider>(logs),
            migrate: false);

        // Act
        await node.StartAsync(ct);

        // Assert
        LoggedError(logs, $"The background jobs scheduler cannot open database '{missingName}': it is missing, offline or being restored, or not created yet. It will start once it can")
            .Should().BeTrue();
        logs.Entries.Should().NotContain(entry => entry.Message.Contains(PasswordIn(fixture.Provider, fixture.ConnectionString)));
    }

    [Fact]
    public async Task Scheduler_that_loses_a_race_while_starting_tries_again_without_reporting_an_outage()
    {
        // Arrange — the scheduler cannot be built once because another node's transaction won a deadlock.
        var ct = TestContext.Current.CancellationToken;
        var deadlock = new SchedulerException(
            "The store failed.",
            new PostgresException("deadlock detected", "ERROR", "ERROR", PostgresErrorCodes.DeadlockDetected));

        // Act
        var (started, logs) = await StartAfterAFailedBuildAsync(deadlock, ct);

        // Assert
        started.Should().BeTrue();
        logs.Entries.Should().Contain(entry => entry.Level == LogLevel.Warning && entry.Message.Contains("lost a race"));
        LoggedError(logs, DeferredStartError).Should().BeFalse();
        logs.Entries.Should().NotContain(entry => entry.Level == LogLevel.Critical);
    }

    [Fact]
    public async Task Scheduler_whose_statement_times_out_after_connecting_retries_as_contention()
    {
        // Arrange — the database answered the probe, then building the scheduler waited too long once, as on a lock
        // another node holds: a wait that ran out, not an outage.
        var ct = TestContext.Current.CancellationToken;
        var lockWait = new SchedulerException("The store failed.", new TimeoutException("The lock was not granted in time."));

        // Act
        var (started, logs) = await StartAfterAFailedBuildAsync(lockWait, ct);

        // Assert
        started.Should().BeTrue();
        logs.Entries.Should().Contain(entry => entry.Level == LogLevel.Warning && entry.Message.Contains("lost a race"));
        LoggedError(logs, DeferredStartError).Should().BeFalse();
    }

    [Fact]
    public async Task Scheduler_whose_database_goes_away_after_the_probe_waits_and_starts()
    {
        // Arrange — the database answers the probe, then the scheduler cannot be built once, as in a failover.
        var ct = TestContext.Current.CancellationToken;
        var failover = new SchedulerException(
            "The store failed.",
            new PostgresException("terminating connection due to administrator command", "FATAL", "FATAL", PostgresErrorCodes.AdminShutdown));

        // Act
        var (started, logs) = await StartAfterAFailedBuildAsync(failover, ct);

        // Assert
        started.Should().BeTrue("the start waits out an outage at any step, not only at the probe");
        LoggedError(logs, DeferredStartError).Should().BeTrue();
        logs.Entries.Should().NotContain(entry => entry.Level == LogLevel.Critical);
    }

    // Starts the hosted service over a factory whose first build fails with the failure given, waits for the scheduler
    // the next build hands back to be started, and stops.
    private async Task<(bool Started, CapturingLoggerProvider Logs)> StartAfterAFailedBuildAsync(
        Exception failure,
        CancellationToken ct)
    {
        var scheduler = Substitute.For<IScheduler>();
        var schedulers = Substitute.For<ISchedulerFactory>();
        schedulers.GetScheduler(Arg.Any<CancellationToken>()).Returns(
            _ => throw failure,
            _ => ValueTask.FromResult(scheduler));
        var logs = new CapturingLoggerProvider();
        using var service = HostedServiceOver(new(fixture.Provider, fixture.ConnectionString), schedulers, logs);

        await service.StartAsync(ct);
        var started = await JobsTestWait.UntilAsync(
            () => Task.FromResult(scheduler.ReceivedCalls().Any(call => call.GetMethodInfo().Name == nameof(IScheduler.Start))),
            TimeSpan.FromSeconds(20),
            ct);
        await service.StopAsync(ct);
        return (started, logs);
    }

    private static bool LoggedError(CapturingLoggerProvider logs, string start) =>
        logs.Entries.Any(entry => entry.Level == LogLevel.Error && entry.Message.StartsWith(start, StringComparison.Ordinal));

    // The hosted service alone, over the fixture's database, with the scheduler factory a test controls.
    private static JobsSchedulerHostedService HostedServiceOver(
        JobsTestNodeDatabase target,
        ISchedulerFactory schedulers,
        CapturingLoggerProvider logs) =>
        HostedServiceOver(new HostedServiceParts(target, schedulers, logs));

    private static JobsSchedulerHostedService HostedServiceOver(HostedServiceParts parts)
    {
        var (target, schedulers, logs) = parts;
        var services = new ServiceCollection()
            .AddKeyedSingleton(QuartzRegistration.SchedulerName, schedulers)
            .AddSingleton(parts.Lifetime ?? Substitute.For<IHostApplicationLifetime>())
            .BuildServiceProvider();
        var isPostgreSql = target.Provider == TestDatabaseProvider.PostgreSql;
        var database = new JobStoreDatabase(target.ConnectionString, isPostgreSql, locksOnInsert: !isPostgreSql);
        var options = Options.Create(new BackgroundJobsOptions());
        return new JobsSchedulerHostedService(
            services,
            database,
            JobHandlerRegistry.Build([]),
            options,
            new JobsShutdownSignal(),
            new LoggerFactory([logs]).CreateLogger<JobsSchedulerHostedService>());
    }

    /// <param name="Target">The database the service probes.</param>
    /// <param name="Schedulers">The scheduler factory the test controls.</param>
    /// <param name="Logs">Where the service's log goes.</param>
    private sealed record HostedServiceParts(JobsTestNodeDatabase Target, ISchedulerFactory Schedulers, CapturingLoggerProvider Logs)
    {
        /// <summary>The host's lifetime, whose stopping signal a test raises; one that never stops when not set.</summary>
        public IHostApplicationLifetime? Lifetime { get; init; }
    }

    private static JobsTestNodeDatabase MissingDatabase(TestDatabaseProvider provider, string connectionString, string name) =>
        provider switch
        {
            TestDatabaseProvider.PostgreSql => new(
                provider, new NpgsqlConnectionStringBuilder(connectionString) { Database = name }.ConnectionString),
            TestDatabaseProvider.SqlServer => new(
                provider, new SqlConnectionStringBuilder(connectionString) { InitialCatalog = name }.ConnectionString),
            _ => throw new ArgumentOutOfRangeException(nameof(provider), provider, "Unsupported test database provider."),
        };

    private static JobsTestNodeDatabase WrongPassword(TestDatabaseProvider provider, string connectionString) =>
        provider switch
        {
            TestDatabaseProvider.PostgreSql => new(
                provider, new NpgsqlConnectionStringBuilder(connectionString) { Password = "wrong" }.ConnectionString),
            TestDatabaseProvider.SqlServer => new(
                provider, new SqlConnectionStringBuilder(connectionString) { Password = "wrong" }.ConnectionString),
            _ => throw new ArgumentOutOfRangeException(nameof(provider), provider, "Unsupported test database provider."),
        };

    private static string PasswordIn(TestDatabaseProvider provider, string connectionString) => provider switch
    {
        TestDatabaseProvider.PostgreSql => new NpgsqlConnectionStringBuilder(connectionString).Password!,
        TestDatabaseProvider.SqlServer => new SqlConnectionStringBuilder(connectionString).Password,
        _ => throw new ArgumentOutOfRangeException(nameof(provider), provider, "Unsupported test database provider."),
    };

    // .invalid never resolves (RFC 2606).
    private static JobsTestNodeDatabase UnresolvableHost(TestDatabaseProvider provider, string password) => provider switch
    {
        TestDatabaseProvider.PostgreSql => new(
            provider, $"Host=jobs-db.invalid;Database=endatix;Username=endatix;Password={password};Timeout=2"),
        TestDatabaseProvider.SqlServer => new(
            provider,
            $"Server=jobs-db.invalid;Database=endatix;User ID=endatix;Password={password};Connect Timeout=2;TrustServerCertificate=True"),
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
