using Endatix.Api.Common;
using Endatix.Api.Infrastructure;
using Endatix.Core.Abstractions;
using Endatix.Core.Abstractions.Authorization;
using Endatix.Core.Common;
using Endatix.Modules.Audience.Contracts;
using Endatix.Modules.Audience.Domain;
using Endatix.Modules.Audience.Features.Properties;
using FastEndpoints;
using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Endatix.Modules.Audience.Endpoints.Audience.Properties;

/// <summary>
/// Creates an audience property on a form.
/// </summary>
public sealed class Create(
    IMediator mediator,
    ITenantContext tenantContext)
    : Endpoint<CreateAudiencePropertyRequest, Results<Created<AudiencePropertyResponse>, ProblemHttpResult>>
{
    public override void Configure()
    {
        Post("forms/{formId}/audience/properties");
        Permissions(Actions.Forms.Edit);
        Summary(summary =>
        {
            summary.Summary = "Create audience property";
            summary.Description =
                "Creates a property. VariableName is derived from Name at create and never changes.";
            summary.Responses[201] = "Property created.";
            summary.Responses[400] = "Invalid input.";
            summary.Responses[404] = "Form not found.";
            summary.Responses[409] = "Variable name already exists on this form.";
        });
        Description(builder => builder
            .Produces<AudiencePropertyResponse>(StatusCodes.Status201Created, "application/json")
            .ProducesProblem(400)
            .ProducesProblem(404)
            .ProducesProblem(409));
    }

    public override async Task<Results<Created<AudiencePropertyResponse>, ProblemHttpResult>> ExecuteAsync(
        CreateAudiencePropertyRequest request,
        CancellationToken ct)
    {
        var result = await mediator.Send(
            new CreatePropertyCommand(
                tenantContext.TenantId,
                request.FormId,
                request.Name ?? string.Empty,
                request.DataType ?? string.Empty,
                string.IsNullOrEmpty(request.ChoicesJson) ? null : request.ChoicesJson,
                request.AllowsOther),
            ct);

        return TypedResultsBuilder
            .MapResult(result, AudiencePropertyResponse.FromDto)
            .SetTypedResults<Created<AudiencePropertyResponse>, ProblemHttpResult>();
    }
}

/// <summary>
/// Validator for create audience property.
/// </summary>
public sealed class CreateAudiencePropertyValidator : Validator<CreateAudiencePropertyRequest>
{
    public CreateAudiencePropertyValidator()
    {
        RuleFor(request => request.FormId).GreaterThan(0);
        RuleFor(request => request.Name)
            .MaximumLength(DataSchemaConstants.MAX_NAME_LENGTH)
            .Must(name => Property.VariableNameError(name) is null)
            .WithMessage((_, name) => Property.VariableNameError(name));
        RuleFor(request => request.DataType)
            .NotEmpty()
            .Must(kind => AudienceDataTypeCodes.IsKnown(kind))
            .WithMessage("Unknown audience data type.");
        RuleFor(request => request.ChoicesJson).ValidJsonString();
    }
}

/// <summary>
/// Request to create an audience property.
/// </summary>
public sealed class CreateAudiencePropertyRequest
{
    public long FormId { get; init; }

    public string? Name { get; init; }

    public string? DataType { get; init; }

    public string? ChoicesJson { get; init; }

    public bool AllowsOther { get; init; }
}
