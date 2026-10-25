using Endatix.Core.Abstractions.BackgroundJobs;
using Endatix.IntegrationTests.Infrastructure.Jobs;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Endatix.IntegrationTests;

/// <summary>
/// A database that refuses every write, as a failover's secondary does and as one read-only on purpose does: an outage,
/// waited out at the start and while a node runs, and named in the log, since waiting never ends the second.
/// </summary>
public sealed partial class JobsSchedulerStartupTests
{
    private const string ReadOnlyStartError = "The background jobs scheduler cannot write to database '";
    private const string ReadOnlyOutcomeWarning = "Background job outcomes cannot be written: the database is read-only";
    private static readonly TimeSpan ReadOnlyNoticed = TimeSpan.FromSeconds(30);

    [Fact]
    public async Task Scheduler_whose_database_is_read_only_waits_saying_so_and_starts_once_it_takes_writes()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        await using var database = await JobsTestDatabase.CreateAsync(fixture, ct);
        await MigrateAsync(database, ct);
        await database.SetReadOnlyAsync(true, ct);
        var invocations = new ProbeInvocations();
        var logs = new CapturingLoggerProvider();
        await using var node = JobsTestNode.Create(
            database,
            configureServices: services =>
            {
                services.AddProbe(invocations);
                services.AddSingleton<ILoggerProvider>(logs);
            },
            migrate: false);
        await node.StartAsync(ct);
        var saidReadOnly = await JobsTestWait.UntilAsync(
            () => Task.FromResult(LoggedError(logs, ReadOnlyStartError)), ReadOnlyNoticed, ct);

        // Act
        await database.SetReadOnlyAsync(false, ct);
        var jobId = await EnqueueOnceTheSchedulerIsBackAsync(node, ct);
        var ran = await JobsTestWait.UntilAsync(() => Task.FromResult(invocations.CountFor(jobId) == 1), Recovery, ct);

        // Assert — told why, with the driver's failure and without the password, and started with nothing to fix.
        saidReadOnly.Should().BeTrue();
        logs.Entries.Should().Contain(entry => entry.Message.StartsWith(ReadOnlyStartError, StringComparison.Ordinal)
            && entry.Message.Contains("read-only, as during a failover, or set read-only on purpose"));
        logs.Entries.Should().NotContain(entry => entry.Message.Contains(PasswordIn(fixture.Provider, fixture.ConnectionString)));
        ran.Should().BeTrue();
        logs.Entries.Should().NotContain(entry => entry.Level == LogLevel.Critical);
    }

    [Fact]
    public async Task Job_whose_outcome_meets_a_read_only_database_is_carried_and_recorded_once_it_takes_writes()
    {
        // Arrange — the job is running when the database stops taking writes, so its outcome cannot be written.
        var ct = TestContext.Current.CancellationToken;
        const string jobType = "ReadOnlyProbe";
        await using var database = await JobsTestDatabase.CreateAsync(fixture, ct);
        var runs = new NamedProbeRuns();
        var logs = new CapturingLoggerProvider();
        await using var node = JobsTestNode.Create(
            database,
            configureServices: services =>
            {
                services.AddScoped<IBackgroundJobHandler>(_ => new NamedProbeHandler(jobType, runs));
                services.AddSingleton<ILoggerProvider>(logs);
            });
        await node.StartAsync(ct);
        var jobId = await node.EnqueueAsync(NamedProbeHandler.Request(jobType, holdMilliseconds: 3000), ct);
        await JobsTestWait.UntilAsync(() => Task.FromResult(runs.HasStarted(jobId)), ReadOnlyNoticed, ct);
        await database.SetReadOnlyAsync(true, ct);
        var saidReadOnly = await JobsTestWait.UntilAsync(
            () => Task.FromResult(logs.Entries.Any(entry => entry.Level == LogLevel.Warning && entry.Message.StartsWith(ReadOnlyOutcomeWarning, StringComparison.Ordinal))),
            ReadOnlyNoticed,
            ct);

        // Act
        await database.SetReadOnlyAsync(false, ct);
        var row = await database.WaitForStatusAsync(jobId, JobStatus.Completed, ct);

        // Assert — the outcome was carried through the outage rather than dropped, and the job ran once.
        saidReadOnly.Should().BeTrue();
        row.AttemptCount.Should().Be(1);
    }
}
