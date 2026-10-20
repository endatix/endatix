using Endatix.Infrastructure.Features.BackgroundJobs;
using Endatix.Infrastructure.Features.Outbox;
using Endatix.Modules.Audience.Features.Forms;
using Endatix.Modules.Audience.Features.TestSubmissions;
using Microsoft.Extensions.DependencyInjection;

namespace Endatix.Modules.Audience.Features.BackgroundJobs;

/// <summary>
/// Registers Audience's outbox work: the inline handlers the relay runs, and the same work as background jobs with
/// the subscriptions that enqueue them. Which one delivers is the relay's switch
/// (<c>Endatix:Outbox:DeliverToJobQueue</c>).
/// </summary>
internal static class AudienceOutboxJobs
{
    // Each job is one short update, as in Reporting: five attempts doubling from 10 seconds.
    private static readonly BackgroundJobTypeDefaults JobDefaults = new()
    {
        MaxAttempts = 5,
        MaxRuntimeMinutes = 10,
        BackoffBaseSeconds = 10,
        BackoffCapSeconds = 600,
        RetentionDays = 3,
        MaxConcurrency = 2,
    };

    public static IServiceCollection AddAudienceOutboxWork(this IServiceCollection services) =>
        services
            .AddOutboxWork<DeleteFormAudienceOutboxHandler, AudienceDeleteFormAudiencePayload>(JobDefaults)
            // Only a test mark frees a link: every other submission.updated (answers, metadata, a new submitter)
            // would only add a job that does nothing.
            .AddOutboxWork<ReleaseLinkOnTestMarkOutboxHandler, AudienceReleaseLinkOnTestMarkPayload>(
                JobDefaults, options => options.OnlyWhenThereIsWork());
}

// The job types are persisted on queued jobs: never rename them.
internal sealed record AudienceDeleteFormAudiencePayload(long OutboxMessageId) : IOutboxJobPayload<AudienceDeleteFormAudiencePayload>
{
    public static string JobType => "AudienceDeleteFormAudience";

    public static AudienceDeleteFormAudiencePayload For(long outboxMessageId) => new(outboxMessageId);
}

internal sealed record AudienceReleaseLinkOnTestMarkPayload(long OutboxMessageId) : IOutboxJobPayload<AudienceReleaseLinkOnTestMarkPayload>
{
    public static string JobType => "AudienceReleaseLinkOnTestMark";

    public static AudienceReleaseLinkOnTestMarkPayload For(long outboxMessageId) => new(outboxMessageId);
}
