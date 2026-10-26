using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Result;
using Endatix.Modules.Audience.Features.People;
using Endatix.Modules.Audience.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Endatix.Modules.Audience.Features.Links;

public sealed record ReissueLinkCommand(long TenantId, long FormId, long MembershipId) : ICommand<Result<IssuedLinkDto>>;

/// <summary>
/// Gives one person's link a new token (<see cref="Domain.AudienceLink.Reissue"/>). Only the token hash is saved, so
/// a first open running meanwhile keeps its claim; its access token carries the old stamp and is refused.
/// </summary>
internal sealed class ReissueLinkHandler(IAudienceDbContext db, IRepository<Form> forms)
    : ICommandHandler<ReissueLinkCommand, Result<IssuedLinkDto>>
{
    private const string PersonNotFound = "Person not found.";
    private const string TestPerson = "A test participant is never sent a link.";
    private const string NoLink = "No link to reissue; generate links first.";

    public async Task<Result<IssuedLinkDto>> Handle(ReissueLinkCommand request, CancellationToken cancellationToken)
    {
        var gate = await TenantFormGate.EnsureAsync(
            new FormGateRequest(forms, request.TenantId, request.FormId, cancellationToken));
        if (!gate.IsSuccess)
        {
            return gate.ToErrorResult<IssuedLinkDto>();
        }

        var isTest = await FormParticipants.OnForm(db, request.TenantId, request.FormId)
            .Where(membership => membership.Id == request.MembershipId)
            .Select(membership => (bool?)membership.IsTest)
            .FirstOrDefaultAsync(cancellationToken);
        return isTest switch
        {
            null => Result.NotFound(PersonNotFound),
            true => Result.Invalid(new ValidationError(TestPerson)),
            false => await ReissueAsync(request, cancellationToken),
        };
    }

    private async Task<Result<IssuedLinkDto>> ReissueAsync(ReissueLinkCommand request, CancellationToken cancellationToken)
    {
        var link = await db.Links.FirstOrDefaultAsync(
            item => item.TenantId == request.TenantId
                && item.FormId == request.FormId
                && item.MembershipId == request.MembershipId,
            cancellationToken);
        if (link is null)
        {
            return Result.NotFound(NoLink);
        }

        var token = link.Reissue();
        await db.SaveChangesAsync(cancellationToken);
        return Result.Success(new IssuedLinkDto(request.MembershipId, token));
    }
}
