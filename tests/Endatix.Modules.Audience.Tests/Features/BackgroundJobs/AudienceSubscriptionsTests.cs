using System.Runtime.CompilerServices;
using Endatix.Infrastructure.Features.BackgroundJobs;
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
    public void AddAudienceOutboxWork_Default_SubscribesExactlyEachInlineHandlersEventsToItsJob()
    {
        // Arrange
        var services = new ServiceCollection();
        var inline = new (IReadOnlyCollection<string> EventTypes, string JobType)[]
        {
            (DeleteFormAudienceOutboxHandler.HandledEventTypes, DeleteFormAudienceOutboxHandler.JobType),
            (ReleaseLinkOnTestMarkOutboxHandler.HandledEventTypes, ReleaseLinkOnTestMarkOutboxHandler.JobType),
        };

        // Act
        services.AddAudienceOutboxWork();
        var subscriptions = services.BuildServiceProvider().GetRequiredService<OutboxSubscriptions>();

        // Assert — every event an inline handler handles is subscribed by its job type, and by nothing else here.
        foreach (var (eventTypes, jobType) in inline)
        {
            eventTypes.Should().AllSatisfy(eventType =>
                subscriptions.For(eventType).Select(subscription => subscription.JobType).Should().Contain(jobType));
        }

        inline.SelectMany(handler => handler.EventTypes).Distinct().SelectMany(subscriptions.For)
            .Select(subscription => subscription.JobType)
            .Should().BeEquivalentTo(inline.Select(handler => handler.JobType));
    }

    [Fact]
    public void AddAudienceOutboxWork_Default_DeclaresTheTunedSettingsForEveryAudienceJobType()
    {
        // Arrange
        var services = new ServiceCollection();
        var tuned = new BackgroundJobTypeDefaults
        {
            MaxAttempts = 5,
            MaxRuntimeMinutes = 10,
            BackoffBaseSeconds = 10,
            BackoffCapSeconds = 600,
            RetentionDays = 3,
            MaxConcurrency = 2,
        };

        // Act
        services.AddAudienceOutboxWork();
        using var provider = services.BuildServiceProvider();

        // Assert — every host gets these, whatever its configuration holds.
        var declared = provider.GetServices<BackgroundJobTypeDefaults>().ToList();
        declared.Select(defaults => defaults.JobType).Should().BeEquivalentTo(
            DeleteFormAudienceOutboxHandler.JobType, ReleaseLinkOnTestMarkOutboxHandler.JobType);
        declared.Should().AllSatisfy(defaults =>
            defaults.Should().BeEquivalentTo(tuned, options => options.Excluding(value => value.JobType)));
    }
}
