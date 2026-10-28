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
    public static IServiceCollection AddAudienceOutboxWork(this IServiceCollection services) =>
        services
            .AddOutboxWork<DeleteFormAudienceOutboxHandler>()
            // Only a test mark frees a link: every other submission.updated (answers, metadata, a new submitter)
            // would only add a job that does nothing.
            .AddOutboxWork<ReleaseLinkOnTestMarkOutboxHandler>(
                onlyWhen: static message => ReleaseLinkOnTestMarkOutboxHandler.Parse(message) is not null);
}
