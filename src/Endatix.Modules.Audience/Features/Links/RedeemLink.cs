using Ardalis.Specification;
using Endatix.Core.Abstractions.Submissions;
using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Result;
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
    IRepository<Form> forms,
    IRepository<Submission> submissions,
    ISubmissionTokenService tokens,
    LinkOpenLock linkLock)
    : ICommandHandler<RedeemLinkCommand, Result<RedeemedLinkDto>>
{
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

    /// <summary>The lock covers only what must not interleave; resuming a claimed link never waits.</summary>
    private async Task<Result<RedeemedLinkDto>> OpenOrResumeAsync(
        AudienceLink link,
        CancellationToken cancellationToken)
    {
        var claimedId = link.SubmissionId;
        if (claimedId is null)
        {
            var opened = await OpenLockedAsync(link, cancellationToken);
            if (opened is not null)
            {
                return opened;
            }

            claimedId = await ClaimedIdAsync(link.Id, cancellationToken);
        }

        return claimedId is long submissionId
            ? await ResumeAsync(link.Id, link.FormId, submissionId, cancellationToken)
            : Result.NotFound(LinkNotFound);
    }

    /// <summary><see langword="null"/> when another open claimed the link first; the caller resumes it.</summary>
    private async Task<Result<RedeemedLinkDto>?> OpenLockedAsync(
        AudienceLink link,
        CancellationToken cancellationToken)
    {
        await using var locked = await linkLock.BeginAsync(link.Id, cancellationToken);
        if (await ClaimedIdAsync(link.Id, cancellationToken) is not null)
        {
            return null;
        }

        var opened = await OpenNewAsync(link, cancellationToken);
        if (opened is { IsSuccess: true })
        {
            await locked.CommitAsync(cancellationToken);
        }

        return opened;
    }

    /// <summary>
    /// Adopts the submission an open left before its claim, or inserts one, then issues its token and claims.
    /// <see langword="null"/> when a writer outside the lock (an older version during a deploy) claimed first:
    /// the uncommitted submission and token roll back with the lock.
    /// </summary>
    private async Task<Result<RedeemedLinkDto>?> OpenNewAsync(AudienceLink link, CancellationToken cancellationToken)
    {
        var form = await OpenFormAsync(link.FormId, cancellationToken);
        if (form is not { IsEnabled: true, DefinitionId: long definitionId })
        {
            return Result.NotFound(form is null ? "Form not found." : LinkNotFound);
        }

        if (!await MembershipIsLiveAsync(link.MembershipId, cancellationToken))
        {
            return Result.NotFound(LinkNotFound);
        }

        if (form.LimitOnePerUser
            && await ExistingSubmissionIdAsync(link, cancellationToken) is long existingId)
        {
            return await ClaimOpenedAsync(link.Id, existingId, created: false, cancellationToken);
        }

        var pending = await submissions.FirstOrDefaultAsync(
            new SubmissionForLinkSpec(link.FormId, link.Id),
            cancellationToken);
        var submission = pending ?? await StartAsync(link, definitionId, cancellationToken);
        return await ClaimOpenedAsync(link.Id, submission.Id, pending is null, cancellationToken);
    }

    /// <summary>Issues the token, then claims. <see langword="null"/> when a writer outside the lock claimed first.</summary>
    private async Task<Result<RedeemedLinkDto>?> ClaimOpenedAsync(
        long linkId,
        long submissionId,
        bool created,
        CancellationToken cancellationToken)
    {
        var redeemed = await RedeemedAsync(submissionId, created, cancellationToken);
        if (!redeemed.IsSuccess)
        {
            return redeemed;
        }

        return await ClaimAsync(linkId, submissionId, cancellationToken) ? redeemed : null;
    }

    private Task<bool> MembershipIsLiveAsync(long membershipId, CancellationToken cancellationToken) =>
        db.Memberships.AnyAsync(membership => membership.Id == membershipId, cancellationToken);

    /// <summary>The submission this person already has on a one-response form.</summary>
    private async Task<long?> ExistingSubmissionIdAsync(AudienceLink link, CancellationToken cancellationToken)
    {
        var submitterId = await (
            from membership in db.Memberships.AsNoTracking()
            join member in db.Members.AsNoTracking() on membership.MemberId equals member.Id
            where membership.Id == link.MembershipId
            select member.SubmitterId).FirstOrDefaultAsync(cancellationToken);
        if (submitterId is not long id)
        {
            return null;
        }

        return await submissions.FirstOrDefaultAsync(
            new SubmissionIdForSubmitterSpec(link.FormId, id),
            cancellationToken);
    }

    /// <summary>Enabled, definition, and one-response, untracked. A tracked form would be stale under the lock.</summary>
    private Task<OpenForm?> OpenFormAsync(long formId, CancellationToken cancellationToken) =>
        forms.FirstOrDefaultAsync(new OpenFormSpec(formId), cancellationToken);

    /// <summary>
    /// A link claimed before tokens were issued under the lock can name a submission without one. Its first
    /// token is issued under the lock, so parallel opens do not each write their own.
    /// </summary>
    private async Task<Result<RedeemedLinkDto>> ResumeAsync(
        long linkId,
        long formId,
        long submissionId,
        CancellationToken cancellationToken)
    {
        var form = await OpenFormAsync(formId, cancellationToken);
        if (form is not { IsEnabled: true })
        {
            return Result.NotFound(LinkNotFound);
        }

        var hasToken = await submissions.FirstOrDefaultAsync(
            new SubmissionHasTokenSpec(submissionId),
            cancellationToken);
        return hasToken switch
        {
            null => Result.NotFound(LinkNotFound),
            true => await RedeemedAsync(submissionId, false, cancellationToken),
            false => await FirstTokenLockedAsync(linkId, submissionId, cancellationToken),
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

    private async Task<Submission> StartAsync(
        AudienceLink link,
        long definitionId,
        CancellationToken cancellationToken)
    {
        var snapshot = await SnapshotAsync(link, DateTime.UtcNow, cancellationToken);
        var submission = Submission.Create(new SubmissionCreateArgs(
            link.TenantId,
            link.FormId,
            definitionId,
            "{}",
            IsComplete: false,
            StartSubmission: true));
        submission.FreezePersonalization(link.Id, snapshot);
        await submissions.AddAsync(submission, cancellationToken);
        await submissions.SaveChangesAsync(cancellationToken);
        return submission;
    }

    private async Task<string> SnapshotAsync(
        AudienceLink link,
        DateTime capturedAt,
        CancellationToken cancellationToken)
    {
        var person = await PersonAsync(link.MembershipId, cancellationToken);
        var cells = await SnapshotCells.LoadAsync(
            db,
            new SnapshotMembership(link.TenantId, link.MembershipId),
            cancellationToken);
        var header = new SnapshotHeader(link.Id, person?.Identifier ?? "", capturedAt, MemberId: person?.Id);
        return PersonalizationSnapshotJson.Write(header, cells);
    }

    private Task<PersonRow?> PersonAsync(long membershipId, CancellationToken cancellationToken) =>
        (
            from membership in db.Memberships
            join member in db.Members on membership.MemberId equals member.Id
            where membership.Id == membershipId
            select new PersonRow(member.Identifier, member.Id))
        .FirstOrDefaultAsync(cancellationToken);

    private sealed record PersonRow(string Identifier, long Id);

    private async Task<Result<RedeemedLinkDto>> RedeemedAsync(
        long submissionId,
        bool created,
        CancellationToken cancellationToken)
    {
        var token = await tokens.ObtainTokenAsync(submissionId, cancellationToken);
        return token.IsSuccess
            ? Result.Success(new RedeemedLinkDto(submissionId, created, token.Value))
            : token.ToErrorResult<RedeemedLinkDto>();
    }
}

/// <summary>
/// The id of the earliest live submission this person already has on the form.
/// <c>FormId</c> seeks <c>IX_Submissions_FormId</c>; <c>SubmitterId</c> is indexed on its own.
/// </summary>
internal sealed class SubmissionIdForSubmitterSpec : Specification<Submission, long?>
{
    public SubmissionIdForSubmitterSpec(long formId, long submitterId) =>
        Query.Where(submission => submission.FormId == formId && submission.SubmitterId == submitterId)
            .OrderBy(submission => submission.Id)
            .Select(submission => (long?)submission.Id);
}

/// <summary>
/// The submission already written for this link, including one whose claim never committed. The form narrows
/// it to <c>IX_Submissions_FormId</c>: <c>AudienceLinkId</c> has no index.
/// </summary>

internal sealed class SubmissionForLinkSpec : Specification<Submission>
{
    public SubmissionForLinkSpec(long formId, long audienceLinkId) =>
        Query.Where(submission => submission.FormId == formId && submission.AudienceLinkId == audienceLinkId)
            .OrderBy(submission => submission.Id);
}

/// <summary>Whether the form can be opened: enabled, its definition, and whether it allows one response.</summary>
internal sealed record OpenForm(bool IsEnabled, long? DefinitionId, bool LimitOnePerUser);

/// <summary>Those three columns only, untracked, so a locked open does not load the form aggregate.</summary>
internal sealed class OpenFormSpec : Specification<Form, OpenForm>
{
    public OpenFormSpec(long formId) =>
        Query.Where(form => form.Id == formId)
            .AsNoTracking()
            .Select(form => new OpenForm(form.IsEnabled, form.ActiveDefinitionId, form.LimitOnePerUser));
}

/// <summary>Whether a live submission has its token, untracked; <see langword="null"/> when there is none.</summary>
internal sealed class SubmissionHasTokenSpec : Specification<Submission, bool?>
{
    public SubmissionHasTokenSpec(long submissionId) =>
        Query.Where(submission => submission.Id == submissionId)
            .Select(submission => (bool?)(submission.Token!.Value != null));
}
