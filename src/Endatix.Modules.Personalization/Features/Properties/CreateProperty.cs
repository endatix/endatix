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
/// Creates an audience property on a form. <see cref="Property.VariableName"/> is fixed at create.
/// </summary>
public sealed record CreatePropertyCommand(
    long TenantId,
    long FormId,
    string Name,
    string DataType,
    string? ChoicesJson = null,
    bool AllowsOther = false) : ICommand<Result<PropertyDto>>;

/// <summary>
/// One audience property on a form.
/// </summary>
public sealed record PropertyDto(
    long Id,
    long FormId,
    string VariableName,
    string Name,
    string DataType,
    int SortOrder,
    long? DataListId,
    string? ChoicesJson,
    bool AllowsOther)
{
    internal static PropertyDto From(Property property) => new(
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

internal sealed class CreatePropertyHandler(
    IPersonalizationDbContext db,
    IRepository<Form> forms)
    : ICommandHandler<CreatePropertyCommand, Result<PropertyDto>>
{
    public async Task<Result<PropertyDto>> Handle(
        CreatePropertyCommand request,
        CancellationToken cancellationToken)
    {
        Result gate = await GateAsync(request, cancellationToken);
        if (!gate.IsSuccess)
        {
            return gate.ToErrorResult<PropertyDto>();
        }

        Result unique = await CheckUniqueAsync(request, cancellationToken);
        if (!unique.IsSuccess)
        {
            return unique.ToErrorResult<PropertyDto>();
        }

        return await CreateAsync(request, cancellationToken);
    }

    private async Task<Result> CheckUniqueAsync(
        CreatePropertyCommand request,
        CancellationToken cancellationToken)
    {
        string variableName = Property.Slugify(request.Name);
        return await EnsureUniqueAsync(request.FormId, variableName, cancellationToken);
    }

    private async Task<Result<PropertyDto>> CreateAsync(
        CreatePropertyCommand request,
        CancellationToken cancellationToken)
    {
        Result<Property> saved = await PersistAsync(request, cancellationToken);
        return saved.IsSuccess
            ? Result<PropertyDto>.Created(PropertyDto.From(saved.Value))
            : saved.ToErrorResult<PropertyDto>();
    }

    private async Task<Result> GateAsync(
        CreatePropertyCommand request,
        CancellationToken cancellationToken)
    {
        Result formGate = await TenantFormGate.EnsureAsync(
            new FormGateRequest(forms, request.TenantId, request.FormId, cancellationToken));
        if (!formGate.IsSuccess)
        {
            return formGate;
        }

        string? inputError = InputError(request);
        return inputError is null
            ? Result.Success()
            : Result.Invalid(new ValidationError(inputError));
    }

    private static string? InputError(CreatePropertyCommand request) =>
        Property.VariableNameError(request.Name)
        ?? (AudienceDataTypeCodes.IsKnown(request.DataType)
            ? null
            : $"Unknown audience data type '{request.DataType}'.");

    private async Task<Result> EnsureUniqueAsync(
        long formId,
        string variableName,
        CancellationToken cancellationToken)
    {
        bool nameTaken = await db.Properties.AnyAsync(
            property => property.FormId == formId && property.VariableName == variableName,
            cancellationToken);
        return nameTaken
            ? Result.Conflict(
                $"An audience property with variable name '{variableName}' already exists on this form.")
            : Result.Success();
    }

    private async Task<Result<Property>> PersistAsync(
        CreatePropertyCommand request,
        CancellationToken cancellationToken)
    {
        Property property = await BuildPropertyAsync(request, cancellationToken);
        db.Properties.Add(property);
        return await SavePropertyAsync(property, cancellationToken);
    }

    private async Task<Property> BuildPropertyAsync(
        CreatePropertyCommand request,
        CancellationToken cancellationToken)
    {
        int sortOrder = await NextSortAsync(request.FormId, cancellationToken);
        return new Property(new PropertyCreateArgs(
            request.TenantId,
            request.FormId,
            request.Name,
            request.DataType,
            sortOrder,
            request.ChoicesJson,
            request.AllowsOther));
    }

    private async Task<Result<Property>> SavePropertyAsync(
        Property property,
        CancellationToken cancellationToken)
    {
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (UniqueIndexViolation.Is(
            ex,
            UniqueIndexViolation.PropertiesVariableName))
        {
            return Result.Conflict(
                $"An audience property with variable name '{property.VariableName}' already exists on this form.");
        }

        return property;
    }

    private async Task<int> NextSortAsync(long formId, CancellationToken cancellationToken)
    {
        int? max = await db.Properties
            .Where(property => property.FormId == formId)
            .Select(property => (int?)property.SortOrder)
            .MaxAsync(cancellationToken);
        return (max ?? -1) + 1;
    }
}
