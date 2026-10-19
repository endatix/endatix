using Endatix.Core.Abstractions.BackgroundJobs;

namespace Endatix.Modules.Audience.Features.BackgroundJobs;

/// <summary>The input of every Audience job: the outbox message whose event it processes.</summary>
internal interface IAudienceOutboxJobPayload : IBackgroundJobPayload
{
    long OutboxMessageId { get; }
}

internal sealed record AudienceDeleteFormAudiencePayload(long OutboxMessageId) : IAudienceOutboxJobPayload
{
    public static string JobType => "AudienceDeleteFormAudience";
}

internal sealed record AudienceReleaseLinkOnTestMarkPayload(long OutboxMessageId) : IAudienceOutboxJobPayload
{
    public static string JobType => "AudienceReleaseLinkOnTestMark";
}
