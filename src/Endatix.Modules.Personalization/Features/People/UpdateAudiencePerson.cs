using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Result;
using Endatix.Modules.Personalization.Domain;
using Endatix.Modules.Personalization.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Endatix.Modules.Personalization.Features.People;

/// <summary>
/// Updates property values for a person on this form.
/// </summary>
public sealed record UpdateAudiencePersonCommand(
    long TenantId,
    long FormId,
    long MembershipId,
    IReadOnlyDictionary<long, string> Values) : ICommand<Result<AudiencePersonDto>>;

internal sealed class UpdateAudiencePersonHandler(
    IPersonalizationDbContext db,
    IRepository<Form> forms)
    : ICommandHandler<UpdateAudiencePersonCommand, Result<AudiencePersonDto>>
{
    public async Task<Result<AudiencePersonDto>> Handle(
        UpdateAudiencePersonCommand request,
        CancellationToken cancellationToken)
    {
        Result gate = await GateAsync(request, cancellationToken);
        if (!gate.IsSuccess)
        {
            return TenantFormGate.MapFailure<AudiencePersonDto>(gate);
        }

        AudienceMembership membership = await db.AudienceMemberships.FirstAsync(
            row => row.Id == request.MembershipId && row.FormId == request.FormId,
            cancellationToken);
        await PersistValuesAsync(request, membership.Id, cancellationToken);
        return Result.Success(await ToDtoAsync(membership, cancellationToken));
    }

    private async Task PersistValuesAsync(
        UpdateAudiencePersonCommand request,
        long membershipId,
        CancellationToken cancellationToken)
    {
        await AudiencePropertyValuesWriter.UpsertAsync(
            new AudienceValueWrite(db, request.TenantId, membershipId, request.Values),
            cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
    }

    private async Task<Result> GateAsync(
        UpdateAudiencePersonCommand request,
        CancellationToken cancellationToken)
    {
        Result formGate = await TenantFormGate.EnsureAsync(
            new FormGateRequest(forms, request.TenantId, request.FormId, cancellationToken));
        if (!formGate.IsSuccess)
        {
            return formGate;
        }

        bool exists = await db.AudienceMemberships.AnyAsync(
            row => row.Id == request.MembershipId && row.FormId == request.FormId,
            cancellationToken);
        return exists
            ? await AudiencePropertyValuesWriter.ValidatePropertyIdsAsync(
                new PropertyIdCheck(db, request.FormId, request.Values.Keys.ToList()),
                cancellationToken)
            : Result.NotFound("Audience membership not found.");
    }

    private async Task<AudiencePersonDto> ToDtoAsync(
        AudienceMembership membership,
        CancellationToken cancellationToken)
    {
        AudienceMember member = await db.AudienceMembers
            .FirstAsync(row => row.Id == membership.AudienceMemberId, cancellationToken);
        Dictionary<long, string> allValues = await db.AudiencePropertyValues
            .Where(value => value.AudienceMembershipId == membership.Id)
            .ToDictionaryAsync(value => value.AudiencePropertyId, value => value.Value, cancellationToken);
        return new AudiencePersonDto(membership.Id, member.Id, member.Identifier, allValues);
    }
}
