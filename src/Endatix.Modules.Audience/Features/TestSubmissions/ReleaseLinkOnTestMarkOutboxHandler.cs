using System.Text.Json;
using Ardalis.Specification;
using Endatix.Core.Entities;
using Endatix.Core.Events;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Infrastructure.Features.Outbox;
using Endatix.Infrastructure.Utils;
using Endatix.Modules.Audience.Persistence;
using Endatix.Outbox.Engine;
using Microsoft.EntityFrameworkCore;

namespace Endatix.Modules.Audience.Features.TestSubmissions;

/// <summary>
/// Frees the personalised link that opened a submission once the submission is marked as test, so the person's link
/// opens a fresh real submission. The token is kept: the person's URL does not change. Only the claim is cleared;
/// redeem skips test submissions when it looks for the link's earlier submission.
/// </summary>
/// <remarks>
/// The submission is re-read rather than trusting the payload: a later mark as real wins over a late message. A test
/// submission deleted before the message is handled (a go-live clean-up) still frees the link. The update matches
/// nothing on a repeat, so a retry is harmless.
/// </remarks>
internal sealed class ReleaseLinkOnTestMarkOutboxHandler(IAudienceDbContext db, IRepository<Submission> submissions)
    : IOutboxIntegrationEventHandler
{
    /// <inheritdoc />
    public IReadOnlyCollection<string> EventTypes { get; } = [SubmissionUpdatedEvent.EventTypeName];

    /// <inheritdoc />
    public async Task HandleAsync(IOutboxMessage message, CancellationToken cancellationToken)
    {
        if (Parse(message) is { } marked && await IsTestAsync(marked, cancellationToken))
        {
            await ReleaseLinkAsync(marked, cancellationToken);
        }
    }

    /// <summary>The submission the message is about, or <see langword="null"/> when its test flag did not change.</summary>
    private static MarkedSubmission? Parse(IOutboxMessage message)
    {
        using var document = JsonDocument.Parse(message.Payload);
        var payload = document.RootElement;
        var changeKind = SubmissionChangeKindsExtensions.ParseWireValue(JsonElementReader.TryGetString(payload, "changeKind"));
        return changeKind.HasFlag(SubmissionChangeKinds.Test)
            ? new MarkedSubmission(message.GetRequiredTenantId(payload), message.GetRequiredIdProp(payload, "submissionId"))
            : null;
    }

    private Task<int> ReleaseLinkAsync(MarkedSubmission marked, CancellationToken cancellationToken) =>
        db.Links
            .Where(link => link.TenantId == marked.TenantId && link.SubmissionId == marked.SubmissionId)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(link => link.SubmissionId, (long?)null)
                    .SetProperty(link => link.OpenedAt, (DateTime?)null),
                cancellationToken);

    private Task<bool> IsTestAsync(MarkedSubmission marked, CancellationToken cancellationToken) =>
        submissions.AnyAsync(new TestSubmissionByIdSpec(marked.TenantId, marked.SubmissionId), cancellationToken);

    private sealed record MarkedSubmission(long TenantId, long SubmissionId);
}

/// <summary>
/// The submission, deleted or not, when it is still marked as test. The query filters are off so a deleted one is
/// seen; the tenant predicate stands in for the tenant filter.
/// </summary>
internal sealed class TestSubmissionByIdSpec : Specification<Submission>
{
    public TestSubmissionByIdSpec(long tenantId, long submissionId) =>
        Query.Where(submission => submission.Id == submissionId
                && submission.TenantId == tenantId
                && submission.IsTestSubmission)
            .IgnoreQueryFilters();
}
