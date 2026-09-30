using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Result;
using Endatix.Modules.Personalization.Domain;
using Endatix.Modules.Personalization.Persistence;
using Endatix.Modules.Personalization.Shared;
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

        AudienceProperty? property = await db.AudienceProperties
            .FirstOrDefaultAsync(
                row => row.Id == request.PropertyId && row.FormId == request.FormId,
                cancellationToken);

        if (property is null)
        {
            return Result.NotFound("Audience property not found.");
        }

        List<AudiencePropertyValue> values = await db.AudiencePropertyValues
            .Where(value => value.AudiencePropertyId == property.Id)
            .ToListAsync(cancellationToken);

        foreach (AudiencePropertyValue value in values)
        {
            value.Delete();
        }

        property.Delete();
        await db.SaveChangesAsync(cancellationToken);
        return Result.Success(property.Id.ToString());
    }
}
