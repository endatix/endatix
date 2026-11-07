using Endatix.Core.Abstractions.BackgroundJobs;
using Endatix.Infrastructure.Features.Outbox;
using Endatix.IntegrationTests.Infrastructure.Jobs;
using Endatix.IntegrationTests.Shared;
using Endatix.Outbox.Engine;
using Microsoft.Extensions.DependencyInjection;

namespace Endatix.IntegrationTests;

/// <summary>
/// A subscriber that throws <see cref="OutboxPermanentFailureException"/>: inline, the relay fails the outbox row on
/// its first attempt; on the job queue, the subscriber's job fails on its first attempt. Any other exception is still
/// retried. Each test runs a web host on a database of its own.
/// </summary>
[Collection(nameof(DbIntegrationTestCollection))]
[Trait("Category", "Infrastructure")]
[Trait("Priority", "P1")]
public sealed class OutboxPermanentFailureTests(DbIntegrationFixture fixture)
{
    private const int PENDING = 0;
    private const int SENT = 1;
    private const int FAILED = 2;
    private const string EVENT_TYPE = "probe.happened";

    [Fact]
    public async Task Inline_permanent_failure_fails_the_row_on_its_first_attempt()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        await using var host = await StartInlineAsync(new OutboxPermanentFailureException("Retired event type."), ct);

        // Act
        await host.InsertMessageAsync(new OutboxRow(401, EVENT_TYPE, TenantId: 5), ct);
        var failed = await host.WaitForMessageAsync(401, message => message.Status == FAILED, ct);

        // Assert
        failed.Should().BeTrue();
        var row = await ReadRetryStateAsync(host, 401, ct);
        row.Should().Be((FAILED, 1, false));
    }

    [Fact]
    public async Task Inline_ordinary_failure_reschedules_the_row()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        await using var host = await StartInlineAsync(new InvalidOperationException("Transient."), ct);

        // Act
        await host.InsertMessageAsync(new OutboxRow(402, EVENT_TYPE, TenantId: 5), ct);
        var retried = await host.WaitForMessageAsync(402, message => message.Attempts == 1, ct);

        // Assert
        retried.Should().BeTrue();
        var row = await ReadRetryStateAsync(host, 402, ct);
        row.Should().Be((PENDING, 1, true));
    }

    [Fact]
    public async Task Job_permanent_failure_fails_the_job_on_its_first_attempt()
    {
        // Arrange — the relay hands the message to the job queue, so the row is sent once the job is queued.
        var ct = TestContext.Current.CancellationToken;
        await using var host = await FanOutHost.StartAsync(
            fixture,
            new FanOutHostSetup(DeliverToJobQueue: true, services => services.AddOutboxWork<PermanentlyFailingWork>()),
            ct);

        // Act
        await host.InsertMessageAsync(new OutboxRow(403, EVENT_TYPE, TenantId: 5), ct);
        var finished = await JobsTestWait.UntilAsync(
            async () => await host.Database.CountAsync(
                $"""SELECT count(*) FROM jobs."BackgroundJobs" WHERE "JobType" = '{PermanentlyFailingWork.JobType}' AND "Status" = {(int)JobStatus.Failed}""",
                ct) == 1,
            TimeSpan.FromSeconds(30),
            ct);

        // Assert — the row was sent when the job was queued; only the job failed.
        finished.Should().BeTrue();
        (await host.ReadMessageAsync(403, ct)).Status.Should().Be(SENT);
        var attempts = await host.Database.QueryAsync(
            $"""SELECT "AttemptCount" FROM jobs."BackgroundJobs" WHERE "JobType" = '{PermanentlyFailingWork.JobType}'""",
            reader => reader.GetInt32(0),
            ct);
        attempts.Should().Equal(1);
    }

    private Task<FanOutHost> StartInlineAsync(Exception failure, CancellationToken ct) =>
        FanOutHost.StartAsync(
            fixture,
            new FanOutHostSetup(
                DeliverToJobQueue: false,
                services => services.AddSingleton<IOutboxIntegrationEventHandler>(new ThrowingHandler(failure)))
            {
                StripsInlineHandlers = true,
            },
            ct);

    private static async Task<(int Status, int Attempts, bool HasNextAttempt)> ReadRetryStateAsync(
        FanOutHost host,
        long id,
        CancellationToken ct) =>
        (await host.Database.QueryAsync(
            $"""SELECT "Status", "Attempts", "NextAttemptAt" FROM "OutboxMessages" WHERE "Id" = {id}""",
            reader => (reader.GetInt32(0), reader.GetInt32(1), !reader.IsDBNull(2)),
            ct)).Single();

    private sealed class ThrowingHandler(Exception failure) : IOutboxIntegrationEventHandler
    {
        public IReadOnlyCollection<string> EventTypes { get; } = [EVENT_TYPE];

        public Task HandleAsync(IOutboxMessage message, CancellationToken cancellationToken) => throw failure;
    }

    internal sealed class PermanentlyFailingWork : IOutboxWork<PermanentlyFailingWork>
    {
        public static string JobType => "TestPermanentlyFailingWork";

        public static IReadOnlyCollection<string> HandledEventTypes { get; } = [EVENT_TYPE];

        public OutboxWork Read(IOutboxMessage message) =>
            OutboxWork.For(_ => throw new OutboxPermanentFailureException("Retired event type."));
    }
}
