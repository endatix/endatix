using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Result;
using Endatix.Modules.Personalization.Contracts;
using Endatix.Modules.Personalization.Domain;
using Endatix.Modules.Personalization.Persistence;
using Endatix.Modules.Personalization.Shared;
using Microsoft.EntityFrameworkCore;

namespace Endatix.Modules.Personalization.Features.Properties;

/// <summary>
/// Creates an audience property on a form. <see cref="AudienceProperty.VariableName"/> is fixed at create.
/// </summary>
public sealed record CreateAudiencePropertyCommand(
    long TenantId,
    long FormId,
    string Name,
    string DataType,
    string? ChoicesJson = null,
    bool AllowsOther = false) : ICommand<Result<AudiencePropertyDto>>;

/// <summary>
/// One audience property on a form.
/// </summary>
public sealed record AudiencePropertyDto(
    long Id,
    long FormId,
    string VariableName,
    string Name,
    string DataType,
    int SortOrder,
    long? DataListId,
    string? ChoicesJson,
    bool AllowsOther);

internal sealed class CreateAudiencePropertyHandler(
    IPersonalizationDbContext db,
    IRepository<Form> forms)
    : ICommandHandler<CreateAudiencePropertyCommand, Result<AudiencePropertyDto>>
{
    public async Task<Result<AudiencePropertyDto>> Handle(
        CreateAudiencePropertyCommand request,
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

        if (!AudienceDataTypeCodes.IsKnown(request.DataType))
        {
            return Result.Invalid(new ValidationError(
                $"Unknown audience data type '{request.DataType}'."));
        }

        string variableName;
        try
        {
            variableName = AudienceProperty.Slugify(request.Name);
        }
        catch (ArgumentException ex)
        {
            return Result.Invalid(new ValidationError(ex.Message));
        }

        bool nameTaken = await db.AudienceProperties.AnyAsync(
            property => property.FormId == request.FormId
                && property.VariableName == variableName,
            cancellationToken);
        if (nameTaken)
        {
            return Result.Conflict(
                $"An audience property with variable name '{variableName}' already exists on this form.");
        }

        int nextSort = await db.AudienceProperties
            .Where(property => property.FormId == request.FormId)
            .Select(property => (int?)property.SortOrder)
            .MaxAsync(cancellationToken) ?? -1;

        AudienceProperty property = new(
            request.TenantId,
            request.FormId,
            request.Name,
            request.DataType,
            nextSort + 1,
            request.ChoicesJson,
            request.AllowsOther);

        db.AudienceProperties.Add(property);
        await db.SaveChangesAsync(cancellationToken);

        return Result.Success(ToDto(property));
    }

    internal static AudiencePropertyDto ToDto(AudienceProperty property) => new(
        property.Id,
        property.FormId,
        property.VariableName,
        property.Name,
        property.DataType,
        property.SortOrder,
        property.DataListId,
        property.ChoicesJson,
        property.AllowsOther);
}
