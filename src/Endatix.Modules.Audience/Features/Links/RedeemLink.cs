using Endatix.Core.Abstractions.Submissions;
using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Result;
using Endatix.Modules.Audience.Domain;
using Endatix.Modules.Audience.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Endatix.Modules.Audience.Features.Links;

public sealed record RedeemedLinkDto(long SubmissionId, bool Created, string AccessToken);

public sealed record RedeemLinkCommand(long FormId, string Token) : ICommand<Result<RedeemedLinkDto>>;

internal sealed class RedeemLinkHandler(
    IAudienceDbContext db,
    IRepository<Form> forms,
    IRepository<Submission> submissions,
    ISubmissionTokenService tokens)
    : ICommandHandler<RedeemLinkCommand, Result<RedeemedLinkDto>>
{
    public async Task<Result<RedeemedLinkDto>> Handle(
        RedeemLinkCommand request,
        CancellationToken cancellationToken)
    {
        var link = await db.Links.FirstOrDefaultAsync(
            item => item.FormId == request.FormId && item.TokenHash == AudienceLink.Hash(request.Token),
            cancellationToken);
        if (link is null)
        {
            return Result.NotFound("Link not found.");
        }

        if (link.SubmissionId is long existingId)
        {
            return await ResumeAsync(existingId, cancellationToken);
        }

        return await OpenAsync(link, cancellationToken);
    }

    private async Task<Result<RedeemedLinkDto>> ResumeAsync(
        long submissionId,
        CancellationToken cancellationToken)
    {
        var submission = await submissions.GetByIdAsync(submissionId, cancellationToken);
        return submission is null
            ? Result.NotFound("Link not found.")
            : await RedeemedAsync(submissionId, false, cancellationToken);
    }

    private async Task<Result<RedeemedLinkDto>> OpenAsync(AudienceLink link, CancellationToken cancellationToken)
    {
        var form = await forms.GetByIdAsync(link.FormId, cancellationToken);
        if (form?.ActiveDefinitionId is not long definitionId)
        {
            return Result.NotFound("Form not found.");
        }

        var submission = await StartAsync(link, definitionId, cancellationToken);
        if (await ClaimAsync(link.Id, submission.Id, cancellationToken))
        {
            return await RedeemedAsync(submission.Id, true, cancellationToken);
        }

        submission.Delete();
        await submissions.SaveChangesAsync(cancellationToken);
        return await ResumeClaimedAsync(link.Id, cancellationToken);
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

    private async Task<Result<RedeemedLinkDto>> ResumeClaimedAsync(
        long linkId,
        CancellationToken cancellationToken)
    {
        var submissionId = await db.Links.AsNoTracking()
            .Where(item => item.Id == linkId)
            .Select(item => item.SubmissionId)
            .FirstOrDefaultAsync(cancellationToken);
        return submissionId is long existingId
            ? await ResumeAsync(existingId, cancellationToken)
            : Result.NotFound("Link not found.");
    }

    private async Task<string> SnapshotAsync(
        AudienceLink link,
        DateTime capturedAt,
        CancellationToken cancellationToken)
    {
        var person = await PersonAsync(link.MembershipId, cancellationToken);
        var cells = await SnapshotCells.LoadAsync(db, link.MembershipId, cancellationToken);
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
