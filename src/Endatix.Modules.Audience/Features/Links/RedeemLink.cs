using Endatix.Core.Abstractions.Submissions;
using Endatix.Core.Entities;
using Endatix.Core.Exceptions;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.Specifications;
using Endatix.Infrastructure.Data;
using Endatix.Infrastructure.Data.Locking;
using Endatix.Modules.Audience.Domain;
using Endatix.Modules.Audience.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Endatix.Modules.Audience.Features.Links;

public sealed record RedeemedLinkDto(long SubmissionId, bool Created, string AccessToken);

public sealed record RedeemLinkCommand(long FormId, string Token) : ICommand<Result<RedeemedLinkDto>>;

/// <summary>
/// The first open of a link, and the first token of a link claimed without one, run under
/// <see cref="LinkOpenLock"/>, so parallel opens of one link take turns. Everything else runs unlocked.
/// </summary>
internal sealed class RedeemLinkHandler(
    IAudienceDbContext db,
    IRepository<Submission> submissions,
    AppDbContext app,
    ISubmissionTokenService tokens,
    LinkOpenLock linkLock,
    LinkOpenGates gates)
    : ICommandHandler<RedeemLinkCommand, Result<RedeemedLinkDto>>
{
    /// <summary>Every refusal reads the same, so an anonymous caller learns nothing about the form or the person.</summary>
    private const string LinkNotFound = "Link not found.";

    public async Task<Result<RedeemedLinkDto>> Handle(
        RedeemLinkCommand request,
        CancellationToken cancellationToken)
    {
        var link = await LinkAsync(request, cancellationToken);
        if (link is null)
        {
            return Result.NotFound(LinkNotFound);
        }

        try
        {
            return await OpenOrResumeAsync(link, cancellationToken);
        }
        catch (TransactionLockTimeoutException)
        {
            return Result.Unavailable("This link is being opened. Try again in a moment.");
        }
    }

    private Task<AudienceLink?> LinkAsync(RedeemLinkCommand request, CancellationToken cancellationToken) =>
        db.Links.AsNoTracking().FirstOrDefaultAsync(
            item => item.FormId == request.FormId && item.TokenHash == AudienceLink.Hash(request.Token),
            cancellationToken);

    /// <summary>
    /// The lock covers only what must not interleave; resuming a claimed link never waits. A claim on a submission
    /// marked as test, deleted or not, is no longer the person's: it is cleared here, without waiting for the outbox,
    /// and the link opens afresh, so the person never resumes a test submission or meets a dead link.
    /// </summary>
    private async Task<Result<RedeemedLinkDto>> OpenOrResumeAsync(
        AudienceLink link,
        CancellationToken cancellationToken)
    {
        if (link.SubmissionId is long claimed)
        {
            var resumed = await ResumeAsync(link.Id, claimed, cancellationToken);
            if (resumed is not null)
            {
                return resumed;
            }

            await ReleaseStaleClaimAsync(link.Id, claimed, cancellationToken);
        }

        var opened = await OpenAsync(link, cancellationToken);
        if (opened is not null)
        {
            return opened;
        }

        // Another open claimed the link first. Its claim is fresh, so a stale one now is a refusal, not a loop.
        return await ClaimedIdAsync(link.Id, cancellationToken) is long submissionId
            ? await ResumeAsync(link.Id, submissionId, cancellationToken) ?? Result.NotFound(LinkNotFound)
            : Result.NotFound(LinkNotFound);
    }

    /// <summary>Matches only while the link still names that submission, so a claim made meanwhile stays.</summary>
    private Task<int> ReleaseStaleClaimAsync(long linkId, long submissionId, CancellationToken cancellationToken) =>
        db.Links
            .Where(item => item.Id == linkId && item.SubmissionId == submissionId)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(item => item.SubmissionId, (long?)null)
                    .SetProperty(item => item.OpenedAt, (DateTime?)null),
                cancellationToken);

    /// <summary>
    /// Checks the gates, then opens under the lock. An insert refused by <c>UX_Submissions_RestrictionKey</c> means
    /// another writer (an on-behalf create) stored this person's response first. The lock rolled ours back, and the
    /// second pass takes that response over. <see langword="null"/> when another open claimed the link first.
    /// </summary>
    private async Task<Result<RedeemedLinkDto>?> OpenAsync(AudienceLink link, CancellationToken cancellationToken)
    {
        var opening = await gates.CheckAsync(link, cancellationToken);
        if (opening is null)
        {
            return Result.NotFound(LinkNotFound);
        }

        try
        {
            return await OpenLockedAsync(opening, cancellationToken);
        }
        catch (DuplicateSubmissionException)
        {
            return await OpenLockedAsync(opening, cancellationToken);
        }
    }

    private async Task<Result<RedeemedLinkDto>?> OpenLockedAsync(
        LinkOpening opening,
        CancellationToken cancellationToken)
    {
        await using var locked = await linkLock.BeginAsync(opening.Link.Id, cancellationToken);
        if (await ClaimedIdAsync(opening.Link.Id, cancellationToken) is not null)
        {
            return null;
        }

        var opened = await OpenNewAsync(opening, cancellationToken);
        if (opened is { IsSuccess: true })
        {
            await locked.CommitAsync(cancellationToken);
        }

        return opened;
    }

    /// <summary>
    /// Takes over the response an earlier open left before its claim, else the person's response on a one-response
    /// form, else starts one. Then issues its token and claims. <see langword="null"/> when a writer outside the lock
    /// (an older version during a deploy) claimed first: everything here rolls back with the lock.
    /// </summary>
    private async Task<Result<RedeemedLinkDto>?> OpenNewAsync(LinkOpening opening, CancellationToken cancellationToken)
    {
        var link = opening.Link;
        var existing = await submissions.FirstOrDefaultAsync(new SubmissionForLinkSpec(link.FormId, link.Id), cancellationToken)
            ?? await PersonsResponseAsync(opening, cancellationToken);
        var submission = existing is null
            ? await StartAsync(opening, cancellationToken)
            : await TakeOverAsync(link, existing, cancellationToken);
        if (submission is null)
        {
            return Result.NotFound(LinkNotFound);
        }

        var redeemed = await RedeemedAsync(submission.Id, existing is null, cancellationToken);
        return !redeemed.IsSuccess || await ClaimAsync(link.Id, submission.Id, cancellationToken) ? redeemed : null;
    }

    /// <summary>The person's response on a one-response form. Test responses do not count, as in Core's gate.</summary>
    private async Task<Submission?> PersonsResponseAsync(LinkOpening opening, CancellationToken cancellationToken) =>
        opening.SubmitterId is long submitterId
            ? await submissions.FirstOrDefaultAsync(
                new SubmissionByFormIdAndSubmitterIdSpec(opening.Link.FormId, submitterId),
                cancellationToken)
            : null;

    /// <summary>
    /// A response this open did not create becomes the link's: it is marked started, and it gets the live audience
    /// values when it has none (an on-behalf create before the person was in the audience). Values it has stay.
    /// The snapshot is read either way: it is the check under the lock that the person is still there.
    /// <see langword="null"/> when the person was removed meanwhile.
    /// </summary>
    private async Task<Submission?> TakeOverAsync(
        AudienceLink link,
        Submission submission,
        CancellationToken cancellationToken)
    {
        var snapshot = await SnapshotAsync(link, cancellationToken);
        if (snapshot is null)
        {
            return null;
        }

        if (submission.PersonalizationSnapshot is null)
        {
            submission.FreezePersonalization(link.Id, snapshot);
        }

        submission.EnsureStarted();
        await submissions.SaveChangesAsync(cancellationToken);
        return submission;
    }

    /// <summary>
    /// A link claimed before tokens were issued under the lock can name a submission without one. Its first
    /// token is issued under the lock, so parallel opens do not each write their own. <see langword="null"/> when the
    /// claim is stale: the submission is a test, live or deleted. A deleted real response keeps the link refused.
    /// </summary>
    private async Task<Result<RedeemedLinkDto>?> ResumeAsync(
        long linkId,
        long submissionId,
        CancellationToken cancellationToken)
    {
        var state = await submissions.FirstOrDefaultAsync(new SubmissionResumeSpec(submissionId), cancellationToken);
        if (state is null && !await IsDeletedTestAsync(submissionId, cancellationToken))
        {
            return Result.NotFound(LinkNotFound);
        }

        return state switch
        {
            null or { IsTest: true } => null,
            { FormIsEnabled: false } => Result.NotFound(LinkNotFound),
            { HasToken: true } => await RedeemedAsync(submissionId, false, cancellationToken),
            _ => await FirstTokenLockedAsync(linkId, submissionId, cancellationToken),
        };
    }

    private async Task<Result<RedeemedLinkDto>> FirstTokenLockedAsync(
        long linkId,
        long submissionId,
        CancellationToken cancellationToken)
    {
        await using var locked = await linkLock.BeginAsync(linkId, cancellationToken);
        var redeemed = await RedeemedAsync(submissionId, false, cancellationToken);
        if (redeemed.IsSuccess)
        {
            await locked.CommitAsync(cancellationToken);
        }

        return redeemed;
    }

    /// <summary>Only the soft-delete filter is lifted; a deleted form still hides its submissions.</summary>
    private Task<bool> IsDeletedTestAsync(long submissionId, CancellationToken cancellationToken) =>
        app.Submissions
            .IgnoreQueryFilters([EndatixQueryFilterNames.SoftDelete])
            .AnyAsync(
                submission => submission.Id == submissionId && submission.IsDeleted && submission.IsTestSubmission,
                cancellationToken);

    private Task<long?> ClaimedIdAsync(long linkId, CancellationToken cancellationToken) =>
        db.Links.AsNoTracking()
            .Where(item => item.Id == linkId)
            .Select(item => item.SubmissionId)
            .FirstOrDefaultAsync(cancellationToken);

    /// <summary>One winner: the update matches only while the link is still unopened.</summary>
    private async Task<bool> ClaimAsync(long linkId, long submissionId, CancellationToken cancellationToken)
    {
        var updated = await db.Links
            .Where(item => item.Id == linkId && item.SubmissionId == null)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(item => item.SubmissionId, submissionId)
                    .SetProperty(item => item.OpenedAt, DateTime.UtcNow),
                cancellationToken);
        return updated == 1;
    }

    /// <summary><see langword="null"/> when the person was removed meanwhile.</summary>
    private async Task<Submission?> StartAsync(LinkOpening opening, CancellationToken cancellationToken)
    {
        var snapshot = await SnapshotAsync(opening.Link, cancellationToken);
        if (snapshot is null)
        {
            return null;
        }

        var submission = NewResponse(opening);
        submission.FreezePersonalization(opening.Link.Id, snapshot);
        await submissions.AddAsync(submission, cancellationToken);
        await submissions.SaveChangesAsync(cancellationToken);
        return submission;
    }

    /// <summary>
    /// On a one-response form the response carries the person's Submitter and the single-response gate, so
    /// <c>UX_Submissions_RestrictionKey</c> refuses a second one from any writer.
    /// </summary>
    private static Submission NewResponse(LinkOpening opening) =>
        Submission.Create(new SubmissionCreateArgs(
            opening.Link.TenantId,
            opening.Link.FormId,
            opening.DefinitionId,
            "{}",
            IsComplete: false,
            SubmitterId: opening.SubmitterId,
            SubmitterDisplayId: opening.SubmitterLabel,
            EnforceSingleSubmissionGate: opening.SubmitterId is not null,
            StartSubmission: true));

    /// <summary>
    /// The live audience values, or <see langword="null"/> when the person was removed. The cells are read before
    /// the person: a removal deletes both in one save, so a person still there means the cells predate it.
    /// </summary>
    private async Task<string?> SnapshotAsync(AudienceLink link, CancellationToken cancellationToken)
    {
        var cells = await SnapshotCells.LoadAsync(
            db,
            new SnapshotMembership(link.TenantId, link.MembershipId),
            cancellationToken);
        var person = await gates.PersonAsync(link.MembershipId, cancellationToken);
        return person is null
            ? null
            : PersonalizationSnapshotJson.Write(
                new SnapshotHeader(
                    link.Id,
                    person.Identifier,
                    DateTime.UtcNow,
                    PersonalizationSources.Link,
                    person.MemberId),
                cells);
    }

    /// <summary>A submission or form deleted meanwhile is the same refusal as any other.</summary>
    private async Task<Result<RedeemedLinkDto>> RedeemedAsync(
        long submissionId,
        bool created,
        CancellationToken cancellationToken)
    {
        var token = await tokens.ObtainTokenAsync(submissionId, cancellationToken);
        if (token.IsSuccess)
        {
            return Result.Success(new RedeemedLinkDto(submissionId, created, token.Value));
        }

        return token.Status == ResultStatus.NotFound
            ? Result.NotFound(LinkNotFound)
            : token.ToErrorResult<RedeemedLinkDto>();
    }
}
