using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Result;
using Endatix.Modules.Personalization.Contracts;
using Endatix.Modules.Personalization.Domain;
using Endatix.Modules.Personalization.Persistence;
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
        Result gate = await GateAsync(request, cancellationToken);
        if (!gate.IsSuccess)
        {
            return TenantFormGate.MapFailure<AudiencePropertyDto>(gate);
        }

        Result<AudienceProperty> created = await CreateAsync(request, cancellationToken);
        return created.IsSuccess
            ? Result.Success(ToDto(created.Value!))
            : TenantFormGate.MapFailure<AudiencePropertyDto>(created);
    }

    private async Task<Result> GateAsync(
        CreateAudiencePropertyCommand request,
        CancellationToken cancellationToken)
    {
        Result formGate = await TenantFormGate.EnsureAsync(
            new FormGateRequest(forms, request.TenantId, request.FormId, cancellationToken));
        if (!formGate.IsSuccess)
        {
            return formGate;
        }

        return AudienceDataTypeCodes.IsKnown(request.DataType)
            ? Result.Success()
            : Result.Invalid(new ValidationError(
                $"Unknown audience data type '{request.DataType}'."));
    }

    private async Task<Result<AudienceProperty>> CreateAsync(
        CreateAudiencePropertyCommand request,
        CancellationToken cancellationToken)
    {
        Result slug = SlugOrInvalid(request.Name);
        if (!slug.IsSuccess)
        {
            return TenantFormGate.MapFailure<AudienceProperty>(slug);
        }

        string variableName = AudienceProperty.Slugify(request.Name);
        Result unique = await EnsureUniqueAsync(request.FormId, variableName, cancellationToken);
        if (!unique.IsSuccess)
        {
            return TenantFormGate.MapFailure<AudienceProperty>(unique);
        }

        AudienceProperty property = await PersistAsync(request, cancellationToken);
        return Result.Success(property);
    }

    private static Result SlugOrInvalid(string name)
    {
        try
        {
            _ = AudienceProperty.Slugify(name);
            return Result.Success();
        }
        catch (ArgumentException)
        {
            return Result.Invalid(new ValidationError("Name does not yield a variable name."));
        }
    }

    private async Task<Result> EnsureUniqueAsync(
        long formId,
        string variableName,
        CancellationToken cancellationToken)
    {
        bool nameTaken = await db.AudienceProperties.AnyAsync(
            property => property.FormId == formId && property.VariableName == variableName,
            cancellationToken);
        return nameTaken
            ? Result.Conflict(
                $"An audience property with variable name '{variableName}' already exists on this form.")
            : Result.Success();
    }

    private async Task<AudienceProperty> PersistAsync(
        CreateAudiencePropertyCommand request,
        CancellationToken cancellationToken)
    {
        int sortOrder = await NextSortAsync(request.FormId, cancellationToken);
        AudienceProperty property = new(new AudiencePropertyCreateArgs(
            request.TenantId,
            request.FormId,
            request.Name,
            request.DataType,
            sortOrder,
            request.ChoicesJson,
            request.AllowsOther));

        db.AudienceProperties.Add(property);
        await db.SaveChangesAsync(cancellationToken);
        return property;
    }

    private async Task<int> NextSortAsync(long formId, CancellationToken cancellationToken)
    {
        int? max = await db.AudienceProperties
            .Where(property => property.FormId == formId)
            .Select(property => (int?)property.SortOrder)
            .MaxAsync(cancellationToken);
        return (max ?? -1) + 1;
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
