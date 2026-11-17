using Endatix.Core.Abstractions.BackgroundJobs;
using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Infrastructure.Features.Outbox;
using Endatix.IntegrationTests.Infrastructure.Jobs;
using Endatix.IntegrationTests.Shared;
using Endatix.Outbox.Engine;
using Microsoft.Extensions.DependencyInjection;

namespace Endatix.IntegrationTests;

/// <summary>
/// Outbox work registered with <c>AddAppLevelOutboxWork</c>, delivered to the job queue: an app-level row becomes a system
/// job, which runs the work with no tenant. Each test runs a web host on a database of its own.
/// </summary>
[Collection(nameof(JobsDbTestCollection))]
[Trait("Category", "Infrastructure")]
[Trait("Priority", "P1")]
[Trait("Lane", "Jobs")]
public sealed class SystemJobsTests(DbIntegrationFixture fixture)
{
    private const int SENT = 1;
    private const long NO_TENANT = 0;
    private const string EVENT_TYPE = "probe.app-level";

    [Fact]
    public async Task App_level_row_runs_its_work_as_a_system_job()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var recorder = new ProbeRecorder();
        await using var host = await FanOutHost.StartAsync(
            fixture,
            new FanOutHostSetup(DeliverToJobQueue: true, services => services
                .AddSingleton(recorder)
                .AddAppLevelOutboxWork<AppLevelProbeWork>()),
            ct);

        // Act
        await host.InsertMessageAsync(new OutboxRow(951, EVENT_TYPE, NO_TENANT), ct);
        var completed = await JobsTestWait.UntilAsync(
            async () => await host.Database.CountAsync(
                $"""SELECT count(*) FROM jobs."BackgroundJobs" WHERE "DedupKey" = '951:{AppLevelProbeWork.JobType}' AND "Status" = {(int)JobStatus.Completed}""",
                ct) == 1,
            TimeSpan.FromSeconds(30),
            ct);

        // Assert — the work ran once in the job and read an app-level repository there.
        completed.Should().BeTrue();
        (await host.ReadMessageAsync(951, ct)).Status.Should().Be(SENT);
        recorder.Runs.Should().Be(1);
        recorder.TenantsSeen.Should().Be(await host.Database.CountAsync("""SELECT count(*) FROM "Tenants" """, ct));
    }

    [Fact]
    public async Task Tenant_row_is_not_turned_into_a_system_job()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var recorder = new ProbeRecorder();
        await using var host = await FanOutHost.StartAsync(
            fixture,
            new FanOutHostSetup(DeliverToJobQueue: true, services => services
                .AddSingleton(recorder)
                .AddAppLevelOutboxWork<AppLevelProbeWork>()),
            ct);

        // Act — the relay refuses the row, so it retries it rather than marking it sent.
        await host.InsertMessageAsync(new OutboxRow(952, EVENT_TYPE, TenantId: 5), ct);
        var retried = await host.WaitForMessageAsync(952, message => message.Attempts >= 1, ct);

        // Assert
        retried.Should().BeTrue();
        (await host.ReadMessageAsync(952, ct)).Status.Should().NotBe(SENT);
        (await host.Database.CountAsync(
            $"""SELECT count(*) FROM jobs."BackgroundJobs" WHERE "JobType" = '{AppLevelProbeWork.JobType}'""", ct))
            .Should().Be(0);
        recorder.Runs.Should().Be(0);
    }

    internal sealed class ProbeRecorder
    {
        private int _runs;

        public int Runs => Volatile.Read(ref _runs);

        public long TenantsSeen { get; private set; } = -1;

        public void Ran(long tenantsSeen)
        {
            TenantsSeen = tenantsSeen;
            Interlocked.Increment(ref _runs);
        }
    }

    internal sealed class AppLevelProbeWork(IRepository<Tenant> tenants, ProbeRecorder recorder)
        : IOutboxWork<AppLevelProbeWork>
    {
        public static string JobType => "TestAppLevelProbe";

        public static IReadOnlyCollection<string> HandledEventTypes { get; } = [EVENT_TYPE];

        public OutboxWork Read(IOutboxMessage message) => OutboxWork.For(async cancellationToken =>
        {
            recorder.Ran(await tenants.CountAsync(cancellationToken));
        });
    }
}
