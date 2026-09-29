using Endatix.Core.Abstractions.BackgroundJobs;
using Endatix.Infrastructure.Features.Outbox;
using Endatix.Outbox.Engine;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace Endatix.Infrastructure.Tests.Features.Outbox;

public sealed class JobQueueIntegrationEventPublisherTests
{
    [Fact]
    public async Task PublishAsync_MultipleSubscribers_CallsEnqueueManyOnce()
    {
        // Arrange
        var queue = Substitute.For<IBackgroundJobQueue>();
        IReadOnlyList<BackgroundJobRequest>? enqueued = null;
        queue.EnqueueManyAsync(Arg.Do<IReadOnlyList<BackgroundJobRequest>>(requests => enqueued = requests), Arg.Any<CancellationToken>())
            .Returns([1L, 2L]);
        await using var provider = Services(queue, services =>
        {
            services.AddOutboxJobSubscription("x.happened", message => new FirstPayload(message.Id));
            services.AddOutboxJobSubscription("x.happened", message => new SecondPayload(message.Id));
        });
        var publisher = provider.GetRequiredService<JobQueueIntegrationEventPublisher>();
        var message = new OutboxMessageStub(77, "x.happened", "{}", 5);

        // Act
        await publisher.PublishAsync(message, TestContext.Current.CancellationToken);

        // Assert
        await queue.Received(1).EnqueueManyAsync(Arg.Any<IReadOnlyList<BackgroundJobRequest>>(), Arg.Any<CancellationToken>());
        enqueued.Should().NotBeNull();
        enqueued.Select(request => (request.JobType, request.TenantId, request.DedupKey, request.CreatedByUserId))
            .Should().Equal(("S1", 5L, "77:S1", (long?)null), ("S2", 5L, "77:S2", (long?)null));
    }

    [Fact]
    public async Task PublishAsync_NoSubscriber_EnqueuesNothing()
    {
        // Arrange
        var queue = Substitute.For<IBackgroundJobQueue>();
        await using var provider = Services(queue, services =>
            services.AddOutboxJobSubscription("x.happened", message => new FirstPayload(message.Id)));
        var publisher = provider.GetRequiredService<JobQueueIntegrationEventPublisher>();

        // Act
        await publisher.PublishAsync(new OutboxMessageStub(78, "y.happened", "{}", 5), TestContext.Current.CancellationToken);

        // Assert
        queue.ReceivedCalls().Should().BeEmpty();
    }

    [Fact]
    public async Task PublishAsync_AppLevelMessageWithoutResolver_ThrowsAndEnqueuesNothing()
    {
        // Arrange
        var queue = Substitute.For<IBackgroundJobQueue>();
        await using var provider = Services(queue, services =>
            services.AddOutboxJobSubscription("x.happened", message => new FirstPayload(message.Id)));
        var publisher = provider.GetRequiredService<JobQueueIntegrationEventPublisher>();

        // Act
        var publish = () => publisher.PublishAsync(
            new OutboxMessageStub(79, "x.happened", "{}", 0), TestContext.Current.CancellationToken);

        // Assert
        (await publish.Should().ThrowAsync<InvalidOperationException>())
            .Which.Message.Should().Be("Outbox message 79 (x.happened) resolved no tenant for subscriber S1.");
        queue.ReceivedCalls().Should().BeEmpty();
    }

    [Fact]
    public async Task PublishAsync_ResolverReadsTenant_UsesResolvedTenant()
    {
        // Arrange
        var queue = Substitute.For<IBackgroundJobQueue>();
        IReadOnlyList<BackgroundJobRequest>? enqueued = null;
        queue.EnqueueManyAsync(Arg.Do<IReadOnlyList<BackgroundJobRequest>>(requests => enqueued = requests), Arg.Any<CancellationToken>())
            .Returns([1L]);
        await using var provider = Services(queue, services =>
            services.AddOutboxJobSubscription("x.happened", message => new FirstPayload(message.Id), _ => 31));
        var publisher = provider.GetRequiredService<JobQueueIntegrationEventPublisher>();

        // Act
        await publisher.PublishAsync(new OutboxMessageStub(80, "x.happened", "{}", 0), TestContext.Current.CancellationToken);

        // Assert
        enqueued.Should().ContainSingle().Which.TenantId.Should().Be(31);
    }

    private static ServiceProvider Services(IBackgroundJobQueue queue, Action<IServiceCollection> subscribe)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddMetrics();
        services.AddSingleton(queue);
        services.AddSingleton(typeof(Microsoft.Extensions.Logging.ILogger<>), typeof(NullLogger<>));
        services.AddScoped<JobQueueIntegrationEventPublisher>();
        subscribe(services);
        return services.BuildServiceProvider();
    }

    internal sealed record FirstPayload(long OutboxMessageId) : IBackgroundJobPayload
    {
        public static string JobType => "S1";
    }

    internal sealed record SecondPayload(long OutboxMessageId) : IBackgroundJobPayload
    {
        public static string JobType => "S2";
    }
}
