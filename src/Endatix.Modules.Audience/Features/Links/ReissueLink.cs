using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Result;
using Endatix.Modules.Audience.Domain;
using Endatix.Modules.Audience.Features.People;
using Endatix.Modules.Audience.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Endatix.Modules.Audience.Features.Links;

/// <summary>Gives one person's link a new token.</summary>
/// <param name="TenantId">The caller's tenant.</param>
/// <param name="FormId">The form.</param>
/// <param name="MembershipId">The person's membership on the form.</param>
public sealed record ReissueLinkCommand(long TenantId, long FormId, long MembershipId)
    : ICommand<Result<IssuedLinkDto>>;

/// <summary>
/// Gives one person's link a new token (<see cref="AudienceLink.Reissue"/>). The write sets only the token hash,
/// and only while the link still has the hash this reissue read: a first open running meanwhile keeps its claim (its
/// access token carries the old stamp and is refused), and a reissue another one overtook is a conflict, so no caller
/// is handed a token that no longer opens the link.
/// </summary>
internal sealed class ReissueLinkHandler(IAudienceDbContext db, IRepository<Form> forms)
    : ICommandHandler<ReissueLinkCommand, Result<IssuedLinkDto>>
{
    private const string PersonNotFound = "Person not found on this form.";
    private const string TestPerson = "A test participant is never sent a link.";
    private const string NoLink = "No link to reissue; generate links first.";
    private const string Overtaken =
        "The link was reissued by another request meanwhile. Reissue it again for a token you can send.";

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

    private async Task<Result<IssuedLinkDto>> ReissueAsync(
        ReissueLinkCommand request,
        CancellationToken cancellationToken)
    {
        var link = await LinkAsync(request, cancellationToken);
        if (link is null)
        {
            return Result.NotFound(NoLink);
        }

        var readHash = link.TokenHash;
        var token = link.Reissue();
        if (!await WriteIfUnchangedAsync(link, readHash, cancellationToken))
        {
            return Result.Conflict(Overtaken);
        }

        return Result.Success(new IssuedLinkDto(request.MembershipId, token));
    }

    private Task<AudienceLink?> LinkAsync(ReissueLinkCommand request, CancellationToken cancellationToken) =>
        db.Links.AsNoTracking().FirstOrDefaultAsync(
            item => item.TenantId == request.TenantId
                && item.FormId == request.FormId
                && item.MembershipId == request.MembershipId,
            cancellationToken);

    /// <summary>Sets only the token hash, and only while the link still has the one this reissue read.</summary>
    private async Task<bool> WriteIfUnchangedAsync(
        AudienceLink link,
        string readHash,
        CancellationToken cancellationToken) =>
        await db.Links
            .Where(item => item.Id == link.Id && item.TokenHash == readHash)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(item => item.TokenHash, link.TokenHash),
                cancellationToken) == 1;
}
