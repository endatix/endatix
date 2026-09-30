using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Result;
using Endatix.Modules.Personalization.Domain;
using Endatix.Modules.Personalization.Persistence;
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
        Result gate = await TenantFormGate.EnsureAsync(
            new FormGateRequest(forms, request.TenantId, request.FormId, cancellationToken));
        if (!gate.IsSuccess)
        {
            return TenantFormGate.MapFailure<AudiencePropertyDto>(gate);
        }

        return await UpdateAsync(request, cancellationToken);
    }

    private async Task<Result<AudiencePropertyDto>> UpdateAsync(
        UpdateAudiencePropertyCommand request,
        CancellationToken cancellationToken)
    {
        Result<AudienceProperty> loaded = await LoadAsync(request, cancellationToken);
        if (!loaded.IsSuccess)
        {
            return TenantFormGate.MapFailure<AudiencePropertyDto>(loaded);
        }

        Result apply = ApplyEdits(loaded.Value!, request);
        if (!apply.IsSuccess)
        {
            return TenantFormGate.MapFailure<AudiencePropertyDto>(apply);
        }

        await db.SaveChangesAsync(cancellationToken);
        return Result.Success(CreateAudiencePropertyHandler.ToDto(loaded.Value!));
    }

    private async Task<Result<AudienceProperty>> LoadAsync(
        UpdateAudiencePropertyCommand request,
        CancellationToken cancellationToken)
    {
        AudienceProperty? property = await db.AudienceProperties.FirstOrDefaultAsync(
            row => row.Id == request.PropertyId && row.FormId == request.FormId,
            cancellationToken);
        return property is null
            ? Result.NotFound("Audience property not found.")
            : Result.Success(property);
    }

    private static Result ApplyEdits(AudienceProperty property, UpdateAudiencePropertyCommand request)
    {
        if (request.Name is not null && !TryRename(property, request.Name))
        {
            return Result.Invalid(new ValidationError("Name is required."));
        }

        if (request.SortOrder is not null)
        {
            property.Reorder(request.SortOrder.Value);
        }

        return Result.Success();
    }

    private static bool TryRename(AudienceProperty property, string name)
    {
        try
        {
            property.Rename(name);
            return true;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }
}
