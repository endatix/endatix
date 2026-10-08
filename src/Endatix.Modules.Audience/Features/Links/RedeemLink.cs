using System.Text.Json;
using Endatix.Core.Abstractions.Submissions;
using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Result;
using Endatix.Modules.Audience.Domain;
using Endatix.Modules.Audience.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Endatix.Modules.Audience.Features.Links;

public sealed record RedeemedLinkDto(long SubmissionId, string Snapshot, bool Created, string AccessToken);

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
        AudienceLink? link = await db.Links.FirstOrDefaultAsync(
            item => item.FormId == request.FormId && item.TokenHash == AudienceLink.Hash(request.Token),
            cancellationToken);
        if (link is null)
        {
            return Result.NotFound("Link not found.");
        }

        if (link.SubmissionId is long existingId)
        {
            return await RedeemedAsync(existingId, await ReadSnapshotAsync(existingId, cancellationToken), false, cancellationToken);
        }

        return await OpenAsync(link, cancellationToken);
    }

    private async Task<Result<RedeemedLinkDto>> OpenAsync(AudienceLink link, CancellationToken cancellationToken)
    {
        Form? form = await forms.GetByIdAsync(link.FormId, cancellationToken);
        if (form?.ActiveDefinitionId is not long definitionId)
        {
            return Result.NotFound("Form not found.");
        }

        string snapshot = await SnapshotAsync(link, cancellationToken);
        Submission submission = Submission.Create(new SubmissionCreateArgs(
            link.TenantId,
            link.FormId,
            definitionId,
            "{}",
            IsComplete: false,
            StartSubmission: true));
        submission.FreezePersonalization(link.Id, snapshot);
        await submissions.AddAsync(submission, cancellationToken);
        await submissions.SaveChangesAsync(cancellationToken);
        link.MarkOpened(submission.Id, DateTime.UtcNow);
        await db.SaveChangesAsync(cancellationToken);
        return await RedeemedAsync(submission.Id, snapshot, true, cancellationToken);
    }

    private async Task<string> SnapshotAsync(AudienceLink link, CancellationToken cancellationToken)
    {
        var cells = await (
            from value in db.PropertyValues
            join property in db.Properties on value.PropertyId equals property.Id
            where value.MembershipId == link.MembershipId
            select new { property.Id, property.VariableName, property.Name, property.DataType, value.Value })
            .ToListAsync(cancellationToken);
        return JsonSerializer.Serialize(new
        {
            schemaVersion = 1,
            audienceLinkId = link.Id,
            values = cells.Select(cell => new
            {
                propertyId = cell.Id,
                variableName = cell.VariableName,
                name = cell.Name,
                dataType = cell.DataType,
                value = cell.Value,
                usage = "variable_only",
            }),
        });
    }

    private async Task<Result<RedeemedLinkDto>> RedeemedAsync(
        long submissionId,
        string snapshot,
        bool created,
        CancellationToken cancellationToken)
    {
        Result<string> token = await tokens.ObtainTokenAsync(submissionId, cancellationToken);
        return token.IsSuccess
            ? Result.Success(new RedeemedLinkDto(submissionId, snapshot, created, token.Value))
            : token.ToErrorResult<RedeemedLinkDto>();
    }

    private async Task<string> ReadSnapshotAsync(long submissionId, CancellationToken cancellationToken)
    {
        Submission? submission = await submissions.GetByIdAsync(submissionId, cancellationToken);
        return submission?.PersonalizationSnapshot ?? "{}";
    }
}
