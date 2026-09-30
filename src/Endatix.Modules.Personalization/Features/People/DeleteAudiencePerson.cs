using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Result;
using Endatix.Modules.Personalization.Domain;
using Endatix.Modules.Personalization.Persistence;
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
        Result gate = await TenantFormGate.EnsureAsync(
            new FormGateRequest(forms, request.TenantId, request.FormId, cancellationToken));
        if (!gate.IsSuccess)
        {
            return TenantFormGate.MapFailure<string>(gate);
        }

        Result<AudienceMembership> loaded = await LoadAsync(request, cancellationToken);
        if (!loaded.IsSuccess)
        {
            return TenantFormGate.MapFailure<string>(loaded);
        }

        await SoftDeleteAsync(loaded.Value!, cancellationToken);
        return Result.Success(loaded.Value!.Id.ToString());
    }

    private async Task<Result<AudienceMembership>> LoadAsync(
        DeleteAudiencePersonCommand request,
        CancellationToken cancellationToken)
    {
        AudienceMembership? membership = await db.AudienceMemberships.FirstOrDefaultAsync(
            row => row.Id == request.MembershipId && row.FormId == request.FormId,
            cancellationToken);
        return membership is null
            ? Result.NotFound("Audience membership not found.")
            : Result.Success(membership);
    }

    private async Task SoftDeleteAsync(AudienceMembership membership, CancellationToken cancellationToken)
    {
        List<AudiencePropertyValue> values = await db.AudiencePropertyValues
            .Where(value => value.AudienceMembershipId == membership.Id)
            .ToListAsync(cancellationToken);

        foreach (AudiencePropertyValue value in values)
        {
            value.Delete();
        }

        membership.Delete();
        await db.SaveChangesAsync(cancellationToken);
    }
}
