using System.Runtime.CompilerServices;
using Endatix.Infrastructure.Features.Outbox;
using Endatix.Modules.Audience.Features.BackgroundJobs;
using Endatix.Modules.Audience.Features.Forms;
using Endatix.Modules.Audience.Features.TestSubmissions;
using Microsoft.Extensions.DependencyInjection;

namespace Endatix.Modules.Audience.Tests.Features.BackgroundJobs;

public sealed class AudienceSubscriptionsTests
{
    [Fact]
    public void Validate_AudienceInlineHandlers_HaveSubscriptionsFromTheirAssembly()
    {
        // Arrange — delivering to the job queue refuses to start unless every inline handler is covered. The
        // check reads only each handler's type and event types, so the handlers are built without dependencies.
        var services = new ServiceCollection();
        services.AddAudienceOutboxWork();
        var subscriptions = services.BuildServiceProvider().GetRequiredService<OutboxSubscriptions>();
        var handlers = new[] { typeof(DeleteFormAudienceOutboxHandler), typeof(ReleaseLinkOnTestMarkOutboxHandler) }
            .Select(type => (IOutboxIntegrationEventHandler)RuntimeHelpers.GetUninitializedObject(type))
            .ToList();

        // Act
        var validate = () => subscriptions.Validate(handlers);

        // Assert
        validate.Should().NotThrow();
    }

    [Fact]
    public void AddAudienceOutboxWork_Default_SubscribesEachHandlersEventsToItsJob()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddAudienceOutboxWork();
        var subscriptions = services.BuildServiceProvider().GetRequiredService<OutboxSubscriptions>();

        // Assert
        DeleteFormAudienceOutboxHandler.HandledEventTypes.Should().AllSatisfy(eventType =>
            subscriptions.For(eventType).Select(subscription => subscription.JobType)
                .Should().Contain(AudienceDeleteFormAudiencePayload.JobType));
        ReleaseLinkOnTestMarkOutboxHandler.HandledEventTypes.Should().AllSatisfy(eventType =>
            subscriptions.For(eventType).Select(subscription => subscription.JobType)
                .Should().Contain(AudienceReleaseLinkOnTestMarkPayload.JobType));
    }
}
