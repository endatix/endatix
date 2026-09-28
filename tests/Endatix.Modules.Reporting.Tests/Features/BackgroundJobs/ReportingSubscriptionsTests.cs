using Endatix.Infrastructure.Features.Outbox;
using Endatix.Modules.Reporting.Features.BackgroundJobs;
using Endatix.Modules.Reporting.Features.Outbox;
using Microsoft.Extensions.DependencyInjection;

namespace Endatix.Modules.Reporting.Tests.Features.BackgroundJobs;

public sealed class ReportingSubscriptionsTests
{
    [Fact]
    public void Subscriptions_MatchInlineHandlerEventTypes()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddReportingOutboxWork();
        var subscriptions = services.BuildServiceProvider().GetRequiredService<OutboxSubscriptions>();
        var inline = new (IReadOnlyCollection<string> EventTypes, string JobType)[]
        {
            (CompileFormSchemaOutboxHandler.HandledEventTypes, ReportingCompileFormSchemaPayload.JobType),
            (FlattenSubmissionOutboxHandler.HandledEventTypes, ReportingFlattenSubmissionPayload.JobType),
            (SeedDefaultExportFormatsOutboxHandler.HandledEventTypes, ReportingSeedDefaultExportFormatsPayload.JobType),
            (SyncFormDeletionOutboxHandler.HandledEventTypes, ReportingSyncFormDeletionPayload.JobType),
            (SyncSubmissionDeletionOutboxHandler.HandledEventTypes, ReportingSyncSubmissionDeletionPayload.JobType),
        };

        // Act
        var subscribed = inline.ToDictionary(
            handler => handler.JobType,
            handler => handler.EventTypes.Where(eventType =>
                subscriptions.For(eventType).Any(subscription => subscription.JobType == handler.JobType)).ToList());

        // Assert — every event an inline handler handles is subscribed by its job type, and by nothing else here.
        foreach (var (eventTypes, jobType) in inline)
        {
            subscribed[jobType].Should().BeEquivalentTo(eventTypes);
        }

        var allInlineEvents = inline.SelectMany(handler => handler.EventTypes).Distinct().ToList();
        allInlineEvents.SelectMany(subscriptions.For).Select(subscription => subscription.JobType)
            .Should().OnlyContain(jobType => jobType.StartsWith("Reporting", StringComparison.Ordinal));
    }

    [Fact]
    public void Validate_ReportingInlineHandlers_HaveSubscriptionsFromTheirAssembly()
    {
        // Arrange — delivering to the job queue refuses to start unless every inline handler is covered.
        var services = new ServiceCollection();
        services.AddReportingOutboxWork();
        var subscriptions = services.BuildServiceProvider().GetRequiredService<OutboxSubscriptions>();
        var handlerTypes = new[]
        {
            typeof(CompileFormSchemaOutboxHandler), typeof(FlattenSubmissionOutboxHandler),
            typeof(SeedDefaultExportFormatsOutboxHandler), typeof(SyncFormDeletionOutboxHandler),
            typeof(SyncSubmissionDeletionOutboxHandler),
        };

        // Act
        var assemblies = handlerTypes.Select(type => type.Assembly).Distinct().ToList();

        // Assert
        assemblies.Should().ContainSingle();
        subscriptions.For("tenant.created").Should().ContainSingle()
            .Which.SourceAssembly.Should().BeSameAs(assemblies[0]);
    }
}
