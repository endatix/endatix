using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Result;
using Endatix.Modules.Personalization.Domain;
using Endatix.Modules.Personalization.Persistence;
using Endatix.Modules.Personalization.Shared;
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
        if (request.TenantId <= 0)
        {
            return Result.Unauthorized("Tenant context is required.");
        }

        Result formResult = await FormAudienceGuard.EnsureFormExistsAsync(
            forms, request.FormId, cancellationToken);
        if (!formResult.IsSuccess)
        {
            return Result.NotFound(formResult.Errors.ToArray());
        }

        AudienceMembership? membership = await db.AudienceMemberships
            .FirstOrDefaultAsync(
                row => row.Id == request.MembershipId && row.FormId == request.FormId,
                cancellationToken);

        if (membership is null)
        {
            return Result.NotFound("Audience membership not found.");
        }

        Result propertyCheck = await AudiencePropertyValuesWriter.ValidatePropertyIdsAsync(
            db, request.FormId, request.Values.Keys, cancellationToken);
        if (!propertyCheck.IsSuccess)
        {
            return Result.Invalid(propertyCheck.ValidationErrors.ToArray());
        }

        await AudiencePropertyValuesWriter.UpsertAsync(
            db, request.TenantId, membership.Id, request.Values, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);

        AudienceMember? member = await db.AudienceMembers
            .FirstOrDefaultAsync(row => row.Id == membership.AudienceMemberId, cancellationToken);

        if (member is null)
        {
            return Result.NotFound("Audience member not found.");
        }

        Dictionary<long, string> allValues = await db.AudiencePropertyValues
            .Where(value => value.AudienceMembershipId == membership.Id)
            .ToDictionaryAsync(
                value => value.AudiencePropertyId,
                value => value.Value,
                cancellationToken);

        return Result.Success(new AudiencePersonDto(
            membership.Id,
            member.Id,
            member.Identifier,
            allValues));
    }
}
