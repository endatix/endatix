using System.Net;
using System.Net.Sockets;
using Endatix.IntegrationTests.Infrastructure.Jobs;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using Npgsql;
using Quartz;

namespace Endatix.IntegrationTests;

/// <summary>A start whose scheduler build times out after the probe connected: a lock wait, or an outage after all.</summary>
public sealed partial class JobsSchedulerStartupTests
{
    private const string LostRaceWarning = "lost a race";

    [Theory]
    [InlineData("Npgsql")]
    [InlineData("SqlClient")]
    public async Task Scheduler_whose_build_times_out_while_connecting_waits_as_an_outage(string driver)
    {
        // Arrange — the probe connected, then a connection Quartz opened timed out in a network blip, which the network
        // is back from by the time anyone asks again. The failure is the driver's own, from a real connection: Npgsql's
        // to an address that never answers, SqlClient's to a listener that never answers its handshake. Neither carries
        // a socket failure, and each has the shape of the driver's command timeout.
        var ct = TestContext.Current.CancellationToken;
        var schedulers = new SchedulerFactoryFailingOnce(async () => new SchedulerException(
            "The store failed.",
            driver == "Npgsql" ? await NpgsqlConnectFailureAsync() : await SqlServerHandshakeTimeoutAsync()));
        var logs = new CapturingLoggerProvider();
        using var service = HostedServiceOver(new(fixture.Provider, fixture.ConnectionString), schedulers, logs);

        // Act
        await service.StartAsync(ct);
        await service.StopAsync(ct);

        // Assert — reported and paced as an outage, not as a lock wait.
        LoggedError(logs, DeferredStartError).Should().BeTrue();
        logs.Entries.Should().NotContain(entry => entry.Message.Contains(LostRaceWarning));
    }

    [Fact]
    public async Task Scheduler_whose_build_times_out_as_the_database_goes_away_waits_as_an_outage()
    {
        // Arrange — the probe connected, then the database is dropped while the build waits, so the wait runs out:
        // asking the probe again finds the database gone, which makes this an outage, not a lock wait.
        var ct = TestContext.Current.CancellationToken;
        var database = await JobsTestDatabase.CreateAsync(fixture, ct);
        await using var dropped = database;
        var schedulers = new SchedulerFactoryFailingOnce(async () =>
        {
            await database.DisposeAsync();
            return new SchedulerException("The store failed.", new TimeoutException("The lock was not granted in time."));
        });
        var logs = new CapturingLoggerProvider();
        using var service = HostedServiceOver(new(database.Provider, database.ConnectionString), schedulers, logs);

        // Act
        await service.StartAsync(ct);
        await service.StopAsync(ct);

        // Assert
        logs.Entries.Should().Contain(entry => entry.Level == LogLevel.Error && entry.Message.Contains("cannot open database"));
        logs.Entries.Should().NotContain(entry => entry.Message.Contains(LostRaceWarning));
    }

    // 10.255.255.1 is not routed, so the connect timeout runs out; a network that rejects it at once fails the connect too.
    private static async Task<Exception> NpgsqlConnectFailureAsync()
    {
        await using var connection = new NpgsqlConnection("Host=10.255.255.1;Port=5432;Username=endatix;Password=endatix;Timeout=1");
        try
        {
            await connection.OpenAsync();
        }
        catch (NpgsqlException failure)
        {
            return failure;
        }

        throw new InvalidOperationException("A connection to an unrouted address opened.");
    }

    // A listener that accepts the connection and never answers SQL Server's pre-login handshake, as a server too busy
    // to does.
    private static async Task<Exception> SqlServerHandshakeTimeoutAsync()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        await using var connection = new SqlConnection(
            $"Server=127.0.0.1,{port};User ID=endatix;Password=endatix;Connect Timeout=1;TrustServerCertificate=True;Pooling=false");
        try
        {
            await connection.OpenAsync();
        }
        catch (SqlException failure)
        {
            return failure;
        }

        throw new InvalidOperationException("A connection to a listener that never answers opened.");
    }

    // Fails the first build with what the test supplies, then builds a scheduler that does nothing.
    private sealed class SchedulerFactoryFailingOnce(Func<Task<Exception>> failure) : ISchedulerFactory
    {
        private readonly IScheduler _scheduler = Substitute.For<IScheduler>();
        private int _builds;

        public async ValueTask<IScheduler> GetScheduler(CancellationToken cancellationToken = default) =>
            Interlocked.Increment(ref _builds) == 1 ? throw await failure() : _scheduler;

        public ValueTask<List<IScheduler>> GetAllSchedulers(CancellationToken cancellationToken = default) =>
            ValueTask.FromResult<List<IScheduler>>([_scheduler]);

        public ValueTask<IScheduler?> LookupScheduler(string schedulerName, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult<IScheduler?>(_scheduler);
    }
}
