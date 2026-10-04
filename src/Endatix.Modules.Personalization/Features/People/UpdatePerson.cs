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
public sealed record UpdatePersonCommand(
    long TenantId,
    long FormId,
    long MembershipId,
    IReadOnlyDictionary<long, string> Values) : ICommand<Result<PersonDto>>;

internal sealed class UpdatePersonHandler(
    IPersonalizationDbContext db,
    IRepository<Form> forms)
    : ICommandHandler<UpdatePersonCommand, Result<PersonDto>>
{
    public async Task<Result<PersonDto>> Handle(
        UpdatePersonCommand request,
        CancellationToken cancellationToken)
    {
        Result<Membership> loaded = await LoadAsync(request, cancellationToken);
        if (!loaded.IsSuccess)
        {
            return loaded.ToErrorResult<PersonDto>();
        }

        Membership membership = loaded.Value!;
        await PropertyValuesWriter.UpsertAsync(
            new PropertyValueWrite(db, request.TenantId, membership.Id, request.Values),
            cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        return Result.Success(await ToDtoAsync(membership, cancellationToken));
    }

    private async Task<Result<Membership>> LoadAsync(
        UpdatePersonCommand request,
        CancellationToken cancellationToken)
    {
        Result formGate = await TenantFormGate.EnsureAsync(
            new FormGateRequest(forms, request.TenantId, request.FormId, cancellationToken));
        if (!formGate.IsSuccess)
        {
            return formGate.ToErrorResult<Membership>();
        }

        Membership? membership = await db.Memberships.FirstOrDefaultAsync(
            row => row.Id == request.MembershipId && row.FormId == request.FormId,
            cancellationToken);
        return membership is null
            ? Result.NotFound("Audience membership not found.")
            : await ValidateValuesAsync(request, membership, cancellationToken);
    }

    private async Task<Result<Membership>> ValidateValuesAsync(
        UpdatePersonCommand request,
        Membership membership,
        CancellationToken cancellationToken)
    {
        Result propertyIds = await PropertyValuesWriter.ValidatePropertyIdsAsync(
            new PropertyIdCheck(db, request.FormId, request.Values.Keys.ToList()),
            cancellationToken);
        return propertyIds.IsSuccess
            ? Result.Success(membership)
            : propertyIds.ToErrorResult<Membership>();
    }

    private async Task<PersonDto> ToDtoAsync(
        Membership membership,
        CancellationToken cancellationToken)
    {
        Member member = await db.Members
            .FirstAsync(row => row.Id == membership.MemberId, cancellationToken);
        Dictionary<long, string> allValues = await db.PropertyValues
            .Where(value => value.MembershipId == membership.Id)
            .ToDictionaryAsync(value => value.PropertyId, value => value.Value, cancellationToken);
        return new PersonDto(membership.Id, member.Id, member.Identifier, allValues);
    }
}
