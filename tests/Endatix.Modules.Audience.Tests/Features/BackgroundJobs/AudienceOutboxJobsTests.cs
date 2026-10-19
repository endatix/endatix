using System.Runtime.CompilerServices;
using Endatix.Core.Entities;
using Endatix.Infrastructure.Features.BackgroundJobs;
using Endatix.Infrastructure.Features.Outbox;
using Endatix.Modules.Audience.Features.BackgroundJobs;
using Endatix.Modules.Audience.Features.Forms;
using Endatix.Modules.Audience.Features.TestSubmissions;
using Microsoft.Extensions.DependencyInjection;

namespace Endatix.Modules.Audience.Tests.Features.BackgroundJobs;

public sealed class AudienceOutboxJobsTests
{
    private static readonly (IReadOnlyCollection<string> EventTypes, string JobType)[] Inline =
    [
        (DeleteFormAudienceOutboxHandler.HandledEventTypes, AudienceDeleteFormAudiencePayload.JobType),
        (ReleaseLinkOnTestMarkOutboxHandler.HandledEventTypes, AudienceReleaseLinkOnTestMarkPayload.JobType),
    ];

    [Fact]
    public void Subscriptions_MatchInlineHandlerEventTypes()
    {
        // Arrange
        var subscriptions = Subscriptions();

        // Act
        var subscribed = Inline.ToDictionary(
            handler => handler.JobType,
            handler => handler.EventTypes.Where(eventType =>
                subscriptions.For(eventType).Any(subscription => subscription.JobType == handler.JobType)).ToList());

        // Assert — every event an inline handler handles is subscribed by its job type, and by nothing else here.
        foreach (var (eventTypes, jobType) in Inline)
        {
            subscribed[jobType].Should().BeEquivalentTo(eventTypes);
        }

        Inline.SelectMany(handler => handler.EventTypes).Distinct().SelectMany(subscriptions.For)
            .Select(subscription => subscription.JobType)
            .Should().OnlyContain(jobType => jobType.StartsWith("Audience", StringComparison.Ordinal));
    }

    [Fact]
    public void Validate_AudienceInlineHandlers_HaveSubscriptionsFromTheirAssembly()
    {
        // Arrange — the check reads only each handler's type and event types, so they are built without dependencies.
        var subscriptions = Subscriptions();
        var handlers = new[] { typeof(DeleteFormAudienceOutboxHandler), typeof(ReleaseLinkOnTestMarkOutboxHandler) }
            .Select(type => (IOutboxIntegrationEventHandler)RuntimeHelpers.GetUninitializedObject(type))
            .ToList();

        // Act
        var validate = () => subscriptions.Validate(handlers);

        // Assert
        validate.Should().NotThrow();
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

        // Assert
        var declared = provider.GetServices<BackgroundJobTypeDefaults>().ToList();
        declared.Select(defaults => defaults.JobType).Should().BeEquivalentTo(
            AudienceDeleteFormAudiencePayload.JobType, AudienceReleaseLinkOnTestMarkPayload.JobType);
        declared.Should().AllSatisfy(defaults =>
            defaults.Should().BeEquivalentTo(tuned, options => options.Excluding(value => value.JobType)));
    }

    [Theory]
    [InlineData("""{"tenantId":"11","submissionId":"2","changeKind":"test"}""", 1)]
    [InlineData("""{"tenantId":"11","submissionId":"2","changeKind":"answers,test"}""", 1)]
    [InlineData("""{"tenantId":"11","submissionId":"2","changeKind":"answers"}""", 0)]
    [InlineData("""{"tenantId":"11","submissionId":"2","changeKind":"metadata"}""", 0)]
    [InlineData("not json", 1)]
    public async Task ReleaseLinkExpander_EnqueuesOnlyTestFlagChangesAndUnreadableMessages(string payload, int jobs)
    {
        // Arrange — an unreadable message still becomes a job, which fails it once instead of the relay retrying.
        var message = new OutboxMessage("submission.updated", payload, 11, DateTime.UtcNow, 1) { Id = 700 };

        // Act
        var subscribers = await new ReleaseLinkOnTestMarkExpander().ExpandAsync(new OutboxMessageView(message), 11, CancellationToken.None);

        // Assert
        subscribers.Select(subscriber => (subscriber.SubscriberKey, subscriber.Payload.OutboxMessageId))
            .Should().Equal(Enumerable.Repeat((AudienceReleaseLinkOnTestMarkPayload.JobType, 700L), jobs));
    }

    private static OutboxSubscriptions Subscriptions()
    {
        var services = new ServiceCollection();
        services.AddAudienceOutboxWork();
        return services.BuildServiceProvider().GetRequiredService<OutboxSubscriptions>();
    }
}
