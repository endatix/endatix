using System.Diagnostics;
using Endatix.Core.Abstractions.BackgroundJobs;
using Endatix.Infrastructure.Features.Outbox;
using Endatix.IntegrationTests.Infrastructure.Jobs;
using Endatix.IntegrationTests.Shared;
using Endatix.Outbox.Engine;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Endatix.IntegrationTests;

/// <summary>
/// The outbox relay delivering to the job queue: one job per subscriber, marked sent, and nothing else done in the
/// relay. Each test runs a web host on a database of its own.
/// </summary>
[Collection(nameof(DbIntegrationTestCollection))]
[Trait("Category", "Infrastructure")]
[Trait("Priority", "P1")]
[Trait("DbSpecific", "PostgreSql")]
public sealed class OutboxFanOutTests(DbIntegrationFixture fixture)
{
    private const string SkipReason =
        "Background jobs are PostgreSQL-only; the module is not registered on this provider.";

    private const int Pending = 0;
    private const int Sent = 1;

    [Fact]
    public async Task Fan_out_enqueues_one_job_per_subscriber_and_marks_sent()
    {
        // Arrange
        Assert.SkipWhen(fixture.Provider != TestDatabaseProvider.PostgreSql, SkipReason);
        var ct = TestContext.Current.CancellationToken;
        await using var host = await FanOutHost.StartAsync(fixture.ConnectionString, JobQueueWith(SubscribeS1AndS2), ct);

        // Act
        await host.InsertMessageAsync(new OutboxRow(77, "x.happened", TenantId: 5), ct);
        var sent = await host.WaitForMessageAsync(77, message => message.Status == Sent, ct);

        // Assert
        sent.Should().BeTrue();
        var jobs = await host.Database.QueryAsync(
            """SELECT "DedupKey", "TenantId", "CreatedByUserId" FROM jobs."BackgroundJobs" ORDER BY "DedupKey" """,
            reader => (reader.GetString(0), reader.GetInt64(1), reader.IsDBNull(2) ? (long?)null : reader.GetInt64(2)),
            ct);
        jobs.Should().Equal(("77:S1", 5L, (long?)null), ("77:S2", 5L, (long?)null));
    }

    [Fact]
    public async Task Redelivered_message_enqueues_no_duplicate_jobs()
    {
        // Arrange — a message whose jobs were enqueued but which was never marked sent is delivered again.
        Assert.SkipWhen(fixture.Provider != TestDatabaseProvider.PostgreSql, SkipReason);
        var ct = TestContext.Current.CancellationToken;
        await using var host = await FanOutHost.StartAsync(fixture.ConnectionString, JobQueueWith(SubscribeS1AndS2), ct);
        var message = new DeliveredMessage(88, "x.happened", "{}", 5);
        await PublishAsync(host, message, ct);

        // Act
        await PublishAsync(host, message, ct);

        // Assert
        (await host.Database.CountAsync("""SELECT count(*) FROM jobs."BackgroundJobs" """, ct)).Should().Be(2);
    }

    [Fact]
    public async Task Message_without_subscribers_is_marked_sent()
    {
        // Arrange
        Assert.SkipWhen(fixture.Provider != TestDatabaseProvider.PostgreSql, SkipReason);
        var ct = TestContext.Current.CancellationToken;
        await using var host = await FanOutHost.StartAsync(fixture.ConnectionString, JobQueueWith(SubscribeS1AndS2), ct);

        // Act
        await host.InsertMessageAsync(new OutboxRow(99, "y.happened", TenantId: 5), ct);
        var sent = await host.WaitForMessageAsync(99, message => message.Status == Sent, ct);

        // Assert
        sent.Should().BeTrue();
        (await host.Database.CountAsync("""SELECT count(*) FROM jobs."BackgroundJobs" """, ct)).Should().Be(0);
    }

    [Fact]
    public async Task Tenantless_message_without_resolver_is_retried_not_enqueued()
    {
        // Arrange
        Assert.SkipWhen(fixture.Provider != TestDatabaseProvider.PostgreSql, SkipReason);
        var ct = TestContext.Current.CancellationToken;
        await using var host = await FanOutHost.StartAsync(fixture.ConnectionString, JobQueueWith(SubscribeS1AndS2), ct);

        // Act
        await host.InsertMessageAsync(new OutboxRow(100, "x.happened", TenantId: 0), ct);
        var retried = await host.WaitForMessageAsync(100, message => message.Attempts == 1, ct);

        // Assert
        retried.Should().BeTrue();
        var message = await host.ReadMessageAsync(100, ct);
        message.Status.Should().Be(Pending);
        message.Attempts.Should().Be(1);
        (await host.Database.CountAsync("""SELECT count(*) FROM jobs."BackgroundJobs" """, ct)).Should().Be(0);
        host.Logs.Entries.Should().Contain(entry => entry.Message.Contains("resolved no tenant for subscriber"));
    }

    [Fact]
    public async Task Relay_does_not_claim_when_fan_out_cannot_enqueue()
    {
        // Arrange — delivery to the job queue is on, but the Jobs module is off.
        Assert.SkipWhen(fixture.Provider != TestDatabaseProvider.PostgreSql, SkipReason);
        var ct = TestContext.Current.CancellationToken;
        await using var host = await FanOutHost.StartAsync(
            fixture.ConnectionString, JobQueueWith(SubscribeS1AndS2) with { JobsModule = false }, ct);

        // Act
        await host.InsertMessageAsync(new OutboxRow(201, "x.happened", TenantId: 5), ct);
        await host.InsertMessageAsync(new OutboxRow(202, "x.happened", TenantId: 5), ct);
        await host.InsertMessageAsync(new OutboxRow(203, "x.happened", TenantId: 5), ct);
        await Task.Delay(TimeSpan.FromSeconds(5), ct);

        // Assert
        foreach (var id in new long[] { 201, 202, 203 })
        {
            var message = await host.ReadMessageAsync(id, ct);
            message.Status.Should().Be(Pending);
            message.LockedBy.Should().BeNull("a paused relay claims nothing");
        }

        host.Logs.Entries.Where(entry => entry.Level == LogLevel.Error && entry.Message.Contains("The outbox relay is paused"))
            .Should().ContainSingle();
    }

    [Fact]
    public async Task Switch_off_keeps_inline_delivery()
    {
        // Arrange
        Assert.SkipWhen(fixture.Provider != TestDatabaseProvider.PostgreSql, SkipReason);
        var ct = TestContext.Current.CancellationToken;
        var inline = new RecordingInlineHandler("submission.completed");
        await using var host = await FanOutHost.StartAsync(
            fixture.ConnectionString,
            new FanOutHostSetup(
                DeliverToJobQueue: false,
                services =>
                {
                    SubscribeS1AndS2(services);
                    services.AddOutboxJobSubscription("submission.completed", message => new SubscriberS1Payload(message.Id));
                    services.AddSingleton<IOutboxIntegrationEventHandler>(inline);
                }),
            ct);

        // Act
        await host.InsertMessageAsync(new OutboxRow(301, "submission.completed", TenantId: 5), ct);
        var sent = await host.WaitForMessageAsync(301, message => message.Status == Sent, ct);

        // Assert
        sent.Should().BeTrue();
        inline.Handled.Should().Equal(301);
        (await host.Database.CountAsync("""SELECT count(*) FROM jobs."BackgroundJobs" """, ct)).Should().Be(0);
    }

    [Fact]
    public async Task Relay_latency_does_not_depend_on_subscriber_duration()
    {
        // Arrange — the subscriber's job holds its slot for a minute.
        Assert.SkipWhen(fixture.Provider != TestDatabaseProvider.PostgreSql, SkipReason);
        var ct = TestContext.Current.CancellationToken;
        await using var host = await FanOutHost.StartAsync(
            fixture.ConnectionString,
            JobQueueWith(services =>
            {
                services.AddOutboxJobSubscription("slow.happened", message => new SlowPayload(message.Id));
                services.AddScoped<IBackgroundJobHandler>(_ => new NamedProbeHandler(SlowPayload.JobType, new NamedProbeRuns()));
            }),
            ct);
        var ids = Enumerable.Range(1_000, 50).Select(id => (long)id).ToList();
        foreach (var id in ids)
        {
            await host.InsertMessageAsync(
                new OutboxRow(id, "slow.happened", TenantId: 5, Payload: """{"holdMilliseconds":60000}"""), ct);
        }

        // Act
        var elapsed = Stopwatch.StartNew();
        var allSent = await JobsTestWait.UntilAsync(
            async () => await host.Database.CountAsync("""SELECT count(*) FROM "OutboxMessages" WHERE "Status" = 1""", ct) == 50,
            TimeSpan.FromSeconds(10),
            ct);

        // Assert
        allSent.Should().BeTrue("every message is sent within 10 s, after {0}", elapsed.Elapsed);
    }

    private static void SubscribeS1AndS2(IServiceCollection services)
    {
        services.AddOutboxJobSubscription("x.happened", message => new SubscriberS1Payload(message.Id));
        services.AddOutboxJobSubscription("x.happened", message => new SubscriberS2Payload(message.Id));
    }

    private static async Task PublishAsync(FanOutHost host, IOutboxMessage message, CancellationToken ct)
    {
        await using var scope = host.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<JobQueueIntegrationEventPublisher>().PublishAsync(message, ct);
    }

    private static FanOutHostSetup JobQueueWith(Action<IServiceCollection> configureServices) =>
        new(DeliverToJobQueue: true, configureServices);

    internal sealed record SubscriberS1Payload(long OutboxMessageId) : IBackgroundJobPayload
    {
        public static string JobType => "S1";
    }

    internal sealed record SubscriberS2Payload(long OutboxMessageId) : IBackgroundJobPayload
    {
        public static string JobType => "S2";
    }

    internal sealed record SlowPayload(long OutboxMessageId, int HoldMilliseconds = 60_000) : IBackgroundJobPayload
    {
        public static string JobType => "SlowSubscriber";
    }

    private sealed record DeliveredMessage(long Id, string EventType, string Payload, long TenantId) : IOutboxMessage
    {
        public DateTimeOffset OccurredAt => DateTimeOffset.UtcNow;
        public int SchemaVersion => 1;
        public int Attempts => 0;
        public string? TraceId => null;
    }

    private sealed class RecordingInlineHandler(string eventType) : IOutboxIntegrationEventHandler
    {
        private readonly List<long> _handled = [];

        public IReadOnlyCollection<string> EventTypes { get; } = [eventType];

        public IReadOnlyList<long> Handled => _handled;

        public Task HandleAsync(IOutboxMessage message, CancellationToken cancellationToken)
        {
            _handled.Add(message.Id);
            return Task.CompletedTask;
        }
    }
}
