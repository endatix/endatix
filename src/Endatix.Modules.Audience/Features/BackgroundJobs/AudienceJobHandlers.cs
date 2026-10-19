using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Modules.Audience.Features.Forms;
using Endatix.Modules.Audience.Features.TestSubmissions;
using Endatix.Outbox.Engine;
using Microsoft.Extensions.Logging;

namespace Endatix.Modules.Audience.Features.BackgroundJobs;

/// <summary>Soft-deletes a deleted form's audience.</summary>
internal sealed class DeleteFormAudienceJobHandler(
    IRepository<OutboxMessage> outboxMessages,
    DeleteFormAudienceOutboxHandler work,
    ILogger<DeleteFormAudienceJobHandler> logger)
    : AudienceOutboxJobHandler<AudienceDeleteFormAudiencePayload, DeleteFormAudienceOutboxHandler.Input>(outboxMessages, logger)
{
    protected override DeleteFormAudienceOutboxHandler.Input Parse(IOutboxMessage message) =>
        DeleteFormAudienceOutboxHandler.Parse(message);

    protected override Task ProcessAsync(DeleteFormAudienceOutboxHandler.Input input, CancellationToken cancellationToken) =>
        work.ProcessAsync(input, cancellationToken);
}

/// <summary>Frees the personalised link of a submission marked as test.</summary>
internal sealed class ReleaseLinkOnTestMarkJobHandler(
    IRepository<OutboxMessage> outboxMessages,
    ReleaseLinkOnTestMarkOutboxHandler work,
    ILogger<ReleaseLinkOnTestMarkJobHandler> logger)
    : AudienceOutboxJobHandler<AudienceReleaseLinkOnTestMarkPayload, ReleaseLinkOnTestMarkOutboxHandler.MarkedSubmission>(outboxMessages, logger)
{
    protected override ReleaseLinkOnTestMarkOutboxHandler.MarkedSubmission? Parse(IOutboxMessage message) =>
        ReleaseLinkOnTestMarkOutboxHandler.Parse(message);

    protected override Task ProcessAsync(
        ReleaseLinkOnTestMarkOutboxHandler.MarkedSubmission input,
        CancellationToken cancellationToken) =>
        work.ProcessAsync(input, cancellationToken);
}
