using System.Text.Json;
using Endatix.Infrastructure.Features.Outbox;
using Endatix.Modules.Audience.Features.TestSubmissions;
using Endatix.Outbox.Engine;

namespace Endatix.Modules.Audience.Features.BackgroundJobs;

/// <summary>
/// Enqueues the release job only for a submission update that changed the test flag, so an answer or metadata edit
/// writes no job.
/// </summary>
/// <remarks>
/// An unreadable message still becomes a job: the job fails it once, where it can be seen, instead of the relay
/// retrying a message no retry can read.
/// </remarks>
internal sealed class ReleaseLinkOnTestMarkExpander : IOutboxSubscriberExpander<AudienceReleaseLinkOnTestMarkPayload>
{
    public Task<IReadOnlyList<OutboxSubscriber<AudienceReleaseLinkOnTestMarkPayload>>> ExpandAsync(
        IOutboxMessage message,
        long tenantId,
        CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<OutboxSubscriber<AudienceReleaseLinkOnTestMarkPayload>>>(
            HasWork(message)
                ? [new(AudienceReleaseLinkOnTestMarkPayload.JobType, new AudienceReleaseLinkOnTestMarkPayload(message.Id))]
                : []);

    private static bool HasWork(IOutboxMessage message)
    {
        try
        {
            return ReleaseLinkOnTestMarkOutboxHandler.Parse(message) is not null;
        }
        catch (Exception exception) when (exception is InvalidOperationException or JsonException)
        {
            return true;
        }
    }
}
