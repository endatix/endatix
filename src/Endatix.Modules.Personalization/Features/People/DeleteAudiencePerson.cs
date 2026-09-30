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
/// Removes a person from this form only. The tenant <see cref="AudienceMember"/> remains.
/// </summary>
public sealed record DeleteAudiencePersonCommand(long TenantId, long FormId, long MembershipId)
    : ICommand<Result<string>>;

internal sealed class DeleteAudiencePersonHandler(
    IPersonalizationDbContext db,
    IRepository<Form> forms)
    : ICommandHandler<DeleteAudiencePersonCommand, Result<string>>
{
    public async Task<Result<string>> Handle(
        DeleteAudiencePersonCommand request,
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

        List<AudiencePropertyValue> values = await db.AudiencePropertyValues
            .Where(value => value.AudienceMembershipId == membership.Id)
            .ToListAsync(cancellationToken);

        foreach (AudiencePropertyValue value in values)
        {
            value.Delete();
        }

        membership.Delete();
        await db.SaveChangesAsync(cancellationToken);
        return Result.Success(membership.Id.ToString());
    }
}
