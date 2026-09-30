using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Result;
using Endatix.Modules.Personalization.Domain;
using Endatix.Modules.Personalization.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Endatix.Modules.Personalization.Features.Properties;

/// <summary>
/// Soft-deletes a property and its value cells on this form.
/// </summary>
public sealed record DeleteAudiencePropertyCommand(long TenantId, long FormId, long PropertyId)
    : ICommand<Result<string>>;

internal sealed class DeleteAudiencePropertyHandler(
    IPersonalizationDbContext db,
    IRepository<Form> forms)
    : ICommandHandler<DeleteAudiencePropertyCommand, Result<string>>
{
    public async Task<Result<string>> Handle(
        DeleteAudiencePropertyCommand request,
        CancellationToken cancellationToken)
    {
        Result gate = await TenantFormGate.EnsureAsync(
            new FormGateRequest(forms, request.TenantId, request.FormId, cancellationToken));
        if (!gate.IsSuccess)
        {
            return TenantFormGate.MapFailure<string>(gate);
        }

        Result<AudienceProperty> loaded = await LoadAsync(request, cancellationToken);
        if (!loaded.IsSuccess)
        {
            return TenantFormGate.MapFailure<string>(loaded);
        }

        await SoftDeleteAsync(loaded.Value!, cancellationToken);
        return Result.Success(loaded.Value!.Id.ToString());
    }

    private async Task<Result<AudienceProperty>> LoadAsync(
        DeleteAudiencePropertyCommand request,
        CancellationToken cancellationToken)
    {
        AudienceProperty? property = await db.AudienceProperties.FirstOrDefaultAsync(
            row => row.Id == request.PropertyId && row.FormId == request.FormId,
            cancellationToken);
        return property is null
            ? Result.NotFound("Audience property not found.")
            : Result.Success(property);
    }

    private async Task SoftDeleteAsync(AudienceProperty property, CancellationToken cancellationToken)
    {
        List<AudiencePropertyValue> values = await db.AudiencePropertyValues
            .Where(value => value.AudiencePropertyId == property.Id)
            .ToListAsync(cancellationToken);

        foreach (AudiencePropertyValue value in values)
        {
            value.Delete();
        }

        property.Delete();
        await db.SaveChangesAsync(cancellationToken);
    }
}
