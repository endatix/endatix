using Endatix.Infrastructure.Features.Outbox;
using Endatix.Outbox.Engine;
using Microsoft.Extensions.DependencyInjection;

namespace Endatix.Infrastructure.Tests.Features.Outbox;

public sealed class OutboxSubscriptionsTests
{
    [Fact]
    public void Validate_InlineHandlerWithoutSubscription_Throws()
    {
        // Arrange — this assembly subscribes to x.happened, but its inline handler also handles z.happened.
        var subscriptions = Build(services =>
            services.AddOutboxJobSubscription(
                "x.happened", message => new JobQueueIntegrationEventPublisherTests.FirstPayload(message.Id)));
        var handler = new InlineHandlerStub(["x.happened", "z.happened"]);

        // Act
        var validate = () => subscriptions.Validate([handler]);

        // Assert
        validate.Should().Throw<InvalidOperationException>()
            .Which.Message.Should().Contain(typeof(InlineHandlerStub).FullName!).And.Contain("z.happened");
    }

    [Fact]
    public void Validate_EveryInlineEventSubscribedFromHandlerAssembly_Succeeds()
    {
        // Arrange
        var subscriptions = Build(services =>
        {
            services.AddOutboxJobSubscription(
                "x.happened", message => new JobQueueIntegrationEventPublisherTests.FirstPayload(message.Id));
            services.AddOutboxJobSubscription(
                "z.happened", message => new JobQueueIntegrationEventPublisherTests.SecondPayload(message.Id));
        });

        // Act
        var validate = () => subscriptions.Validate([new InlineHandlerStub(["x.happened", "z.happened"])]);

        // Assert
        validate.Should().NotThrow();
    }

    [Fact]
    public void For_EventType_ReturnsSubscriptionsInRegistrationOrder()
    {
        // Arrange
        var subscriptions = Build(services =>
        {
            services.AddOutboxJobSubscription(
                "x.happened", message => new JobQueueIntegrationEventPublisherTests.FirstPayload(message.Id));
            services.AddOutboxJobSubscription(
                "x.happened", message => new JobQueueIntegrationEventPublisherTests.SecondPayload(message.Id));
        });

        // Act
        var forEvent = subscriptions.For("x.happened");

        // Assert
        forEvent.Select(subscription => subscription.JobType).Should().Equal("S1", "S2");
        subscriptions.For("other").Should().BeEmpty();
    }

    private static OutboxSubscriptions Build(Action<IServiceCollection> subscribe)
    {
        var services = new ServiceCollection();
        subscribe(services);
        return services.BuildServiceProvider().GetRequiredService<OutboxSubscriptions>();
    }
}
