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
/// Renames and/or reorders a property. Never changes <see cref="AudienceProperty.VariableName"/>.
/// </summary>
public sealed record UpdateAudiencePropertyCommand(
    long TenantId,
    long FormId,
    long PropertyId,
    string? Name,
    int? SortOrder) : ICommand<Result<AudiencePropertyDto>>;

internal sealed class UpdateAudiencePropertyHandler(
    IPersonalizationDbContext db,
    IRepository<Form> forms)
    : ICommandHandler<UpdateAudiencePropertyCommand, Result<AudiencePropertyDto>>
{
    public async Task<Result<AudiencePropertyDto>> Handle(
        UpdateAudiencePropertyCommand request,
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

        if (request.Name is not null)
        {
            try
            {
                property.Rename(request.Name);
            }
            catch (ArgumentException ex)
            {
                return Result.Invalid(new ValidationError(ex.Message));
            }
        }

        if (request.SortOrder is not null)
        {
            property.Reorder(request.SortOrder.Value);
        }

        await db.SaveChangesAsync(cancellationToken);
        return Result.Success(CreateAudiencePropertyHandler.ToDto(property));
    }
}
