using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Result;
using Endatix.Modules.Personalization.Domain;
using Endatix.Modules.Personalization.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Endatix.Modules.Personalization.Features.Properties;

/// <summary>
/// Renames and/or reorders a property. Never changes <see cref="Property.VariableName"/>.
/// </summary>
public sealed record UpdatePropertyCommand(
    long TenantId,
    long FormId,
    long PropertyId,
    string? Name,
    int? SortOrder) : ICommand<Result<PropertyDto>>;

internal sealed class UpdatePropertyHandler(
    IPersonalizationDbContext db,
    IRepository<Form> forms)
    : ICommandHandler<UpdatePropertyCommand, Result<PropertyDto>>
{
    public async Task<Result<PropertyDto>> Handle(
        UpdatePropertyCommand request,
        CancellationToken cancellationToken)
    {
        Result gate = await TenantFormGate.EnsureAsync(
            new FormGateRequest(forms, request.TenantId, request.FormId, cancellationToken));
        if (!gate.IsSuccess)
        {
            return gate.ToErrorResult<PropertyDto>();
        }

        return await UpdateAsync(request, cancellationToken);
    }

    private async Task<Result<PropertyDto>> UpdateAsync(
        UpdatePropertyCommand request,
        CancellationToken cancellationToken)
    {
        Result<Property> loaded = await LoadAsync(request, cancellationToken);
        if (!loaded.IsSuccess)
        {
            return loaded.ToErrorResult<PropertyDto>();
        }

        Result apply = ApplyEdits(loaded.Value!, request);
        if (!apply.IsSuccess)
        {
            return apply.ToErrorResult<PropertyDto>();
        }

        await db.SaveChangesAsync(cancellationToken);
        return Result.Success(PropertyDto.From(loaded.Value!));
    }

    private async Task<Result<Property>> LoadAsync(
        UpdatePropertyCommand request,
        CancellationToken cancellationToken)
    {
        Property? property = await db.Properties.FirstOrDefaultAsync(
            row => row.Id == request.PropertyId && row.FormId == request.FormId,
            cancellationToken);
        return property is null
            ? Result.NotFound("Audience property not found.")
            : Result.Success(property);
    }

    private static Result ApplyEdits(Property property, UpdatePropertyCommand request)
    {
        string? nameError = request.Name is null ? null : Property.NameError(request.Name);
        if (nameError is not null)
        {
            return Result.Invalid(new ValidationError(nameError));
        }

        if (request.Name is not null)
        {
            property.Rename(request.Name);
        }

        if (request.SortOrder is not null)
        {
            property.Reorder(request.SortOrder.Value);
        }

        return Result.Success();
    }
}
