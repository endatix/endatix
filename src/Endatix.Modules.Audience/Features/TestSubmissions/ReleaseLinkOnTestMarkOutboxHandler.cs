using System.Text.Json;
using Endatix.Core.Events;
using Endatix.Infrastructure.Data;
using Endatix.Infrastructure.Features.Outbox;
using Endatix.Infrastructure.Utils;
using Endatix.Modules.Audience.Persistence;
using Endatix.Outbox.Engine;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Endatix.Modules.Audience.Features.TestSubmissions;

/// <summary>
/// Frees the personalised link that opened a submission once the submission is marked as test, so the person's link
/// opens a fresh real submission. The token is kept: the person's URL does not change. Only the claim is cleared.
/// Redeem frees such a link itself when it gets there first, so this handler only tidies the link early.
/// </summary>
/// <remarks>
/// The submission is re-read rather than trusting the payload: a later mark as real wins over a late message. The
/// re-read locks the submission's row until the link is cleared, so a mark as real cannot commit in between. A test
/// submission deleted before the message is handled (a go-live clean-up) still frees the link. The update matches
/// nothing on a repeat, so a retry is harmless.
/// </remarks>
internal sealed class ReleaseLinkOnTestMarkOutboxHandler(IAudienceDbContext db, AppDbContext app)
    : IOutboxWork<ReleaseLinkOnTestMarkOutboxHandler>
{
    public static string JobType => "AudienceReleaseLinkOnTestMark";

    /// <summary>The event types this handler, and its job, run on.</summary>
    public static IReadOnlyCollection<string> HandledEventTypes { get; } = [SubmissionUpdatedEvent.EventTypeName];

    /// <inheritdoc />
    public OutboxWork Read(IOutboxMessage message) =>
        OutboxWork.If(Parse(message), ProcessAsync);

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

    /// <summary>Frees the submission's link when the submission is still marked as test.</summary>
    /// <remarks>
    /// <see cref="AppDbContext"/> joins the audience connection and transaction, as in a link open, so the
    /// submission's row lock and the link update commit together.
    /// </remarks>
    public async Task ProcessAsync(MarkedSubmission marked, CancellationToken cancellationToken)
    {
        var audience = ((DbContext)db).Database;
        await using var transaction = await audience.BeginTransactionAsync(cancellationToken);
        app.Database.SetDbConnection(audience.GetDbConnection());
        await app.Database.UseTransactionAsync(transaction.GetDbTransaction(), cancellationToken);
        try
        {
            if (await LockIfStillTestAsync(marked, cancellationToken))
            {
                await ReleaseLinkAsync(marked, cancellationToken);
            }

            await transaction.CommitAsync(cancellationToken);
        }
        finally
        {
            await app.Database.UseTransactionAsync(null, CancellationToken.None);
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
    /// Rewrites the flag with the value it has, which takes the row's write lock: a mark as real waits for this
    /// transaction, and one that committed first leaves no row to match. Deleted or not, so a clean-up that beat the
    /// message still frees the link. The tenant predicate stands in for the tenant filter; every other filter stays on.
    /// </summary>
    private async Task<bool> LockIfStillTestAsync(MarkedSubmission marked, CancellationToken cancellationToken) =>
        await app.Submissions
            .IgnoreQueryFilters([EndatixQueryFilterNames.SoftDelete, EndatixQueryFilterNames.Tenant])
            .Where(submission => submission.Id == marked.SubmissionId
                && submission.TenantId == marked.TenantId
                && submission.IsTestSubmission)
            .ExecuteUpdateAsync(setters => setters.SetProperty(submission => submission.IsTestSubmission, true),
                cancellationToken) > 0;

    internal sealed record MarkedSubmission(long TenantId, long SubmissionId);
}
