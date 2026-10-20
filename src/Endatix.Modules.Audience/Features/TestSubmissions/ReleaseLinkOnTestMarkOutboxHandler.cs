using System.Text.Json;
using Endatix.Core.Events;
using Endatix.Infrastructure.Data;
using Endatix.Infrastructure.Features.Outbox;
using Endatix.Infrastructure.Utils;
using Endatix.Modules.Audience.Persistence;
using Endatix.Outbox.Engine;
using Microsoft.EntityFrameworkCore;

namespace Endatix.Modules.Audience.Features.TestSubmissions;

/// <summary>
/// Frees the personalised link that opened a submission once the submission is marked as test, so the person's link
/// opens a fresh real submission. The token is kept: the person's URL does not change. Only the claim is cleared.
/// Redeem frees such a link itself when it gets there first, so this handler only tidies the link early.
/// </summary>
/// <remarks>
/// The submission is re-read rather than trusting the payload: a later mark as real wins over a late message. A test
/// submission deleted before the message is handled (a go-live clean-up) still frees the link. The update matches
/// nothing on a repeat, so a retry is harmless.
/// </remarks>
internal sealed class ReleaseLinkOnTestMarkOutboxHandler(IAudienceDbContext db, AppDbContext app)
    : IOutboxIntegrationEventHandler
{
    /// <summary>The event types this handler, and its job, run on.</summary>
    public static IReadOnlyCollection<string> HandledEventTypes { get; } = [SubmissionUpdatedEvent.EventTypeName];

    /// <inheritdoc />
    public IReadOnlyCollection<string> EventTypes => HandledEventTypes;

    /// <inheritdoc />
    public Task HandleAsync(IOutboxMessage message, CancellationToken cancellationToken) =>
        ProcessAsync(Parse(message), cancellationToken);

    /// <summary>The submission the message is about, or <see langword="null"/> when its test flag did not change.</summary>
    /// <exception cref="InvalidOperationException">The message lacks its tenant or submission id.</exception>
    /// <exception cref="JsonException">The payload is not JSON.</exception>
    public static MarkedSubmission? Parse(IOutboxMessage message)
    {
        using var document = JsonDocument.Parse(message.Payload);
        var payload = document.RootElement;
        var changeKind = SubmissionChangeKindsExtensions.ParseWireValue(JsonElementReader.TryGetString(payload, "changeKind"));
        return changeKind.HasFlag(SubmissionChangeKinds.Test)
            ? new MarkedSubmission(message.GetRequiredTenantId(payload), message.GetRequiredIdProp(payload, "submissionId"))
            : null;
    }

    public async Task ProcessAsync(MarkedSubmission? marked, CancellationToken cancellationToken)
    {
        if (marked is not null && await IsTestAsync(marked, cancellationToken))
        {
            await ReleaseLinkAsync(marked, cancellationToken);
        }
    }

    private Task<int> ReleaseLinkAsync(MarkedSubmission marked, CancellationToken cancellationToken) =>
        db.Links
            .Where(link => link.TenantId == marked.TenantId && link.SubmissionId == marked.SubmissionId)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(link => link.SubmissionId, (long?)null)
                    .SetProperty(link => link.OpenedAt, (DateTime?)null),
                cancellationToken);

    /// <summary>
    /// Deleted or not, so a clean-up that beat the message still frees the link. The tenant predicate stands in for
    /// the tenant filter; every other filter stays on.
    /// </summary>
    private Task<bool> IsTestAsync(MarkedSubmission marked, CancellationToken cancellationToken) =>
        app.Submissions
            .IgnoreQueryFilters([EndatixQueryFilterNames.SoftDelete, EndatixQueryFilterNames.Tenant])
            .AnyAsync(
                submission => submission.Id == marked.SubmissionId
                    && submission.TenantId == marked.TenantId
                    && submission.IsTestSubmission,
                cancellationToken);

    internal sealed record MarkedSubmission(long TenantId, long SubmissionId);
}
