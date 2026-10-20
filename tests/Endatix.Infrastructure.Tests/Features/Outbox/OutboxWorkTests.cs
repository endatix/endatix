using System.Text.Json;
using Endatix.Core.Abstractions.BackgroundJobs;
using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Core.Specifications;
using Endatix.Infrastructure.Features.BackgroundJobs;
using Endatix.Infrastructure.Features.Outbox;
using Endatix.Outbox.Engine;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace Endatix.Infrastructure.Tests.Features.Outbox;

public sealed class OutboxWorkTests
{
    private const long TenantA = 11;
    private const long MessageId = 700;

    private static readonly BackgroundJobTypeDefaults Defaults = new() { MaxAttempts = 3 };

    private readonly IRepository<OutboxMessage> _outbox = Substitute.For<IRepository<OutboxMessage>>();
    private readonly IBackgroundJobQueue _queue = Substitute.For<IBackgroundJobQueue>();
    private readonly List<OutboxMessage> _messages = [];
    private IReadOnlyList<BackgroundJobRequest> _enqueued = [];

    public OutboxWorkTests()
    {
        _outbox
            .FirstOrDefaultAsync(Arg.Any<OutboxMessageByIdForTenantSpec>(), Arg.Any<CancellationToken>())
            .Returns(call => call.Arg<OutboxMessageByIdForTenantSpec>().Evaluate(_messages).FirstOrDefault());
        _queue
            .EnqueueManyAsync(
                Arg.Do<IReadOnlyList<BackgroundJobRequest>>(requests => _enqueued = requests),
                Arg.Any<CancellationToken>())
            .Returns(call => [.. call.Arg<IReadOnlyList<BackgroundJobRequest>>().Select((_, index) => (long)index)]);
    }

    [Fact]
    public void AddOutboxWork_OneCall_RegistersInlineHandlerJobHandlerDefaultsAndSubscriptions()
    {
        // Arrange
        using var provider = Build();
        using var scope = provider.CreateScope();

        // Act
        var inline = scope.ServiceProvider.GetServices<IOutboxIntegrationEventHandler>().ToList();
        var jobHandler = scope.ServiceProvider.GetRequiredKeyedService<IBackgroundJobHandler>(WorkPayload.JobType);
        var defaults = provider.GetServices<BackgroundJobTypeDefaults>().ToList();
        var subscriptions = provider.GetRequiredService<OutboxSubscriptions>();

        // Assert — the inline handler is the very instance the job reuses within a scope.
        inline.Should().ContainSingle().Which.Should().BeSameAs(scope.ServiceProvider.GetRequiredService<WorkStub>());
        jobHandler.JobType.Should().Be(WorkPayload.JobType);
        defaults.Should().ContainSingle().Which.Should().BeEquivalentTo(Defaults with { JobType = WorkPayload.JobType });
        WorkStub.HandledEventTypes.Should().AllSatisfy(eventType =>
            subscriptions.For(eventType).Should().ContainSingle().Which.JobType.Should().Be(WorkPayload.JobType));
        var validate = () => subscriptions.Validate(inline);
        validate.Should().NotThrow();
    }

    [Fact]
    public async Task ExecuteAsync_MessageGone_FailsWithoutRetry()
    {
        // Arrange — no row: the outbox cleanup removed it.
        using var provider = Build();

        // Act
        var (result, work) = await RunJobAsync(provider, TenantA);

        // Assert — a failure result, not a throw, so the job is not retried.
        result.ValidationErrors.Should().ContainSingle()
            .Which.ErrorMessage.Should().Be(OutboxJobFailures.MessageGone);
        work.Ran.Should().BeFalse();
    }

    [Fact]
    public async Task ExecuteAsync_MessageOfOtherTenant_FailsAsGone()
    {
        // Arrange — the job is tenant A's; the row is not.
        Seed("""{"tenantId":"12","work":"run"}""", 12);
        using var provider = Build();

        // Act
        var (result, work) = await RunJobAsync(provider, TenantA);

        // Assert
        result.ValidationErrors.Should().ContainSingle()
            .Which.ErrorMessage.Should().Be(OutboxJobFailures.MessageGone);
        work.Ran.Should().BeFalse();
    }

    [Theory]
    [InlineData("""{"tenantId":"12","work":"run"}""")]
    [InlineData("""{"work":"run"}""")]
    [InlineData("not json")]
    public async Task ExecuteAsync_UnreadableMessage_FailsWithoutRetry(string payload)
    {
        // Arrange — the row is tenant A's, but its payload names another tenant, lacks one, or is not JSON.
        Seed(payload, TenantA);
        using var provider = Build();

        // Act
        var (result, work) = await RunJobAsync(provider, TenantA);

        // Assert
        result.ValidationErrors.Should().ContainSingle()
            .Which.ErrorMessage.Should().Be(OutboxJobFailures.MessageUnreadable);
        work.Ran.Should().BeFalse();
    }

    [Fact]
    public async Task ExecuteAsync_NothingToDo_CompletesWithoutWork()
    {
        // Arrange
        Seed($$"""{"tenantId":"{{TenantA}}","work":"none"}""", TenantA);
        using var provider = Build();

        // Act
        var (result, work) = await RunJobAsync(provider, TenantA);

        // Assert
        result.IsSuccess.Should().BeTrue();
        work.Ran.Should().BeFalse();
    }

    [Fact]
    public async Task ExecuteAsync_ReadableMessage_RunsTheWork()
    {
        // Arrange
        Seed($$"""{"tenantId":"{{TenantA}}","work":"run"}""", TenantA);
        using var provider = Build();

        // Act
        var (result, work) = await RunJobAsync(provider, TenantA);

        // Assert
        result.IsSuccess.Should().BeTrue();
        work.Ran.Should().BeTrue();
    }

    [Fact]
    public async Task ExecuteAsync_WorkThrows_ThrowsSoTheJobRetries()
    {
        // Arrange — a transient problem in the work itself, even one the parser's exceptions would name.
        Seed($$"""{"tenantId":"{{TenantA}}","work":"throw"}""", TenantA);
        using var provider = Build();

        // Act
        var run = () => RunJobAsync(provider, TenantA);

        // Assert
        await run.Should().ThrowAsync<InvalidOperationException>().WithMessage("transient");
    }

    [Fact]
    public async Task ExecuteAsync_AppLevel_ReadsTheRowWithoutTenant()
    {
        // Arrange — the row is app-level; the job belongs to the tenant the payload names.
        Seed($$"""{"newTenantId":"{{TenantA}}","work":"run"}""", 0);
        using var provider = Build(options => options.AppLevel(WorkStub.ReadNewTenant));

        // Act
        var (result, work) = await RunJobAsync(provider, TenantA);

        // Assert
        result.IsSuccess.Should().BeTrue();
        work.Ran.Should().BeTrue();
    }

    [Fact]
    public async Task ExecuteAsync_AppLevelPayloadOfOtherTenant_FailsAsGone()
    {
        // Arrange — an app-level row is readable by any job; only its payload says whose it is.
        Seed("""{"newTenantId":"12","work":"run"}""", 0);
        using var provider = Build(options => options.AppLevel(WorkStub.ReadNewTenant));

        // Act
        var (result, work) = await RunJobAsync(provider, TenantA);

        // Assert
        result.ValidationErrors.Should().ContainSingle()
            .Which.ErrorMessage.Should().Be(OutboxJobFailures.MessageGone);
        work.Ran.Should().BeFalse();
    }

    [Fact]
    public async Task ExecuteAsync_ReadThrowsAnythingElse_ThrowsSoTheJobRetries()
    {
        // Arrange — only the parser's own exceptions mean the message is unreadable.
        Seed($$"""{"tenantId":"{{TenantA}}"}""", TenantA);
        using var provider = Build();

        // Act
        var run = () => RunJobAsync(provider, TenantA);

        // Assert
        await run.Should().ThrowAsync<KeyNotFoundException>();
    }

    [Fact]
    public async Task ExecuteAsync_AnotherWorkIsAppLevel_ReadsThisWorksRowUnderTheJobTenant()
    {
        // Arrange — the app-level setting is the other work's alone.
        Seed($$"""{"tenantId":"{{TenantA}}","work":"run"}""", TenantA);
        using var provider = Build(services => services.AddOutboxWork<AppLevelWorkStub, AppLevelWorkPayload>(
            Defaults, options => options.AppLevel(WorkStub.ReadNewTenant)));

        // Act
        var (result, work) = await RunJobAsync(provider, TenantA);

        // Assert
        result.IsSuccess.Should().BeTrue();
        work.Ran.Should().BeTrue();
    }

    [Fact]
    public void Validate_WorkRegisteredInlineOnly_ThrowsThoughItsAssemblySubscribesTheEvent()
    {
        // Arrange — another job of this assembly subscribes x.happened, but none is this work's.
        var services = new ServiceCollection();
        services.AddInlineOutboxHandler<WorkStub>();
        services.AddOutboxJobSubscription(
            "x.happened", message => new JobQueueIntegrationEventPublisherTests.FirstPayload(message.Id));
        var subscriptions = services.BuildServiceProvider().GetRequiredService<OutboxSubscriptions>();

        // Act
        var validate = () => subscriptions.Validate([new WorkStub()]);

        // Assert
        validate.Should().Throw<InvalidOperationException>().Which.Message.Should().Contain(typeof(WorkStub).FullName!);
    }

    [Fact]
    public async Task PublishAsync_Default_EnqueuesOneJobOfTheMessageWithAStableDedupKey()
    {
        // Arrange
        using var provider = Build();

        // Act
        await PublishAsync(provider, new OutboxMessageStub(MessageId, "x.happened", "{}", TenantA));

        // Assert — the wire shape and dedup key of jobs queued before this registration existed.
        var request = _enqueued.Should().ContainSingle().Subject;
        (request.JobType, request.TenantId, request.DedupKey)
            .Should().Be((WorkPayload.JobType, TenantA, $"{MessageId}:{WorkPayload.JobType}"));
        request.PayloadJson.Should().Be($$"""{"outboxMessageId":{{MessageId}}}""");
    }

    [Fact]
    public async Task PublishAsync_AppLevel_EnqueuesTheJobForTheTenantThePayloadNames()
    {
        // Arrange
        using var provider = Build(options => options.AppLevel(WorkStub.ReadNewTenant));

        // Act
        await PublishAsync(provider, new OutboxMessageStub(MessageId, "x.happened", """{"newTenantId":"31"}""", 0));

        // Assert
        _enqueued.Should().ContainSingle().Which.TenantId.Should().Be(31);
    }

    [Fact]
    public async Task PublishAsync_AppLevelUnreadable_ThrowsSoTheRelayRetries()
    {
        // Arrange
        using var provider = Build(options => options.AppLevel(WorkStub.ReadNewTenant));

        // Act
        var publish = () => PublishAsync(provider, new OutboxMessageStub(MessageId, "x.happened", "not json", 0));

        // Assert — an unreadable tenant resolves to none, which fails the publish rather than enqueue a tenantless job.
        await publish.Should().ThrowAsync<InvalidOperationException>();
        _enqueued.Should().BeEmpty();
    }

    [Theory]
    [InlineData("""{"tenantId":"11","work":"run"}""", 1)]
    [InlineData("""{"tenantId":"11","work":"none"}""", 0)]
    [InlineData("not json", 1)]
    public async Task PublishAsync_OnlyWhenThereIsWork_EnqueuesUnlessTheMessageNeedsNothing(string payload, int expectedJobs)
    {
        // Arrange — an unreadable message is still enqueued, so its job fails visibly.
        using var provider = Build(options => options.OnlyWhenThereIsWork());

        // Act
        await PublishAsync(provider, new OutboxMessageStub(MessageId, "x.happened", payload, TenantA));

        // Assert
        _enqueued.Select(request => request.DedupKey)
            .Should().Equal(Enumerable.Repeat($"{MessageId}:{WorkPayload.JobType}", expectedJobs));
    }

    private ServiceProvider Build(Action<OutboxWorkOptions>? configure = null) => Build(_ => { }, configure);

    private ServiceProvider Build(Action<IServiceCollection> addMore, Action<OutboxWorkOptions>? configure = null)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddMetrics();
        services.AddSingleton(_outbox);
        services.AddSingleton(_queue);
        services.AddScoped<JobQueueIntegrationEventPublisher>();
        services.AddOutboxWork<WorkStub, WorkPayload>(Defaults, configure);
        addMore(services);
        return services.BuildServiceProvider();
    }

    private void Seed(string payload, long tenantId) =>
        _messages.Add(new OutboxMessage("x.happened", payload, tenantId, DateTime.UtcNow, 1) { Id = MessageId });

    private static async Task<(Core.Infrastructure.Result.Result Result, WorkStub Work)> RunJobAsync(
        IServiceProvider provider,
        long tenantId)
    {
        using var scope = provider.CreateScope();
        var handler = scope.ServiceProvider.GetRequiredKeyedService<IBackgroundJobHandler>(WorkPayload.JobType);
        var job = new BackgroundJobContext(1, WorkPayload.JobType, tenantId,
            BackgroundJobPayloadSerializer.Serialize(new WorkPayload(MessageId)), 1);
        var result = await handler.ExecuteAsync(job, TestContext.Current.CancellationToken);
        return (result, scope.ServiceProvider.GetRequiredService<WorkStub>());
    }

    private static async Task PublishAsync(IServiceProvider provider, IOutboxMessage message)
    {
        using var scope = provider.CreateScope();
        await scope.ServiceProvider.GetRequiredService<JobQueueIntegrationEventPublisher>()
            .PublishAsync(message, TestContext.Current.CancellationToken);
    }

    internal sealed record WorkPayload(long OutboxMessageId) : IOutboxJobPayload<WorkPayload>
    {
        public static string JobType => "TestWork";

        public static WorkPayload For(long outboxMessageId) => new(outboxMessageId);
    }

    internal sealed record AppLevelWorkPayload(long OutboxMessageId) : IOutboxJobPayload<AppLevelWorkPayload>
    {
        public static string JobType => "TestAppLevelWork";

        public static AppLevelWorkPayload For(long outboxMessageId) => new(outboxMessageId);
    }

    /// <summary>A second work on another event, registered app-level.</summary>
    internal sealed class AppLevelWorkStub : IOutboxWork
    {
        public static IReadOnlyCollection<string> HandledEventTypes { get; } = ["y.happened"];

        public IReadOnlyCollection<string> EventTypes => HandledEventTypes;

        public OutboxWork Read(IOutboxMessage message) => OutboxWork.None;

        public Task HandleAsync(IOutboxMessage message, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    /// <summary>
    /// Reads <c>work</c>: <c>run</c> runs, <c>none</c> has nothing to do, <c>throw</c> fails as the work would on
    /// a transient problem.
    /// </summary>
    internal sealed class WorkStub : IOutboxWork
    {
        public static IReadOnlyCollection<string> HandledEventTypes { get; } = ["x.happened"];

        public IReadOnlyCollection<string> EventTypes => HandledEventTypes;

        public bool Ran { get; private set; }

        public static long ReadNewTenant(IOutboxMessage message)
        {
            using var document = JsonDocument.Parse(message.Payload);
            return message.GetRequiredIdProp(document.RootElement, "newTenantId");
        }

        public OutboxWork Read(IOutboxMessage message)
        {
            using var document = JsonDocument.Parse(message.Payload);
            var payload = document.RootElement;
            if (message.TenantId != 0)
            {
                message.GetRequiredTenantId(payload);
            }

            return payload.GetProperty("work").GetString() switch
            {
                "none" => OutboxWork.None,
                "throw" => OutboxWork.For(_ => throw new InvalidOperationException("transient")),
                _ => OutboxWork.For(_ =>
                {
                    Ran = true;
                    return Task.CompletedTask;
                }),
            };
        }

        public Task HandleAsync(IOutboxMessage message, CancellationToken cancellationToken) =>
            Read(message).RunAsync(cancellationToken);
    }
}
