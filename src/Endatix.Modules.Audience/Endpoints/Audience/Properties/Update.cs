using Endatix.Api.Infrastructure;
using Endatix.Core.Abstractions;
using Endatix.Core.Abstractions.Authorization;
using Endatix.Core.Common;
using Endatix.Modules.Audience.Domain;
using Endatix.Modules.Audience.Features.Properties;
using FastEndpoints;
using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Endatix.Modules.Audience.Endpoints.Audience.Properties;

/// <summary>
/// Renames and/or reorders an audience property.
/// </summary>
public sealed class Update(
    IMediator mediator,
    ITenantContext tenantContext)
    : Endpoint<UpdateAudiencePropertyRequest, Results<Ok<AudiencePropertyResponse>, ProblemHttpResult>>
{
    public override void Configure()
    {
        Patch("forms/{formId}/audience/properties/{propertyId}");
        Permissions(Actions.Forms.Edit);
        Summary(summary =>
        {
            summary.Summary = "Update audience property";
            summary.Description =
                "Renames the label and/or changes sort order. VariableName is never changed.";
            summary.Responses[200] = "Property updated.";
            summary.Responses[404] = "Form or property not found.";
        });
        Description(builder => builder
            .Produces<AudiencePropertyResponse>(200, "application/json")
            .ProducesProblem(400)
            .ProducesProblem(404));
    }

    public override async Task<Results<Ok<AudiencePropertyResponse>, ProblemHttpResult>> ExecuteAsync(
        UpdateAudiencePropertyRequest request,
        CancellationToken ct)
    {
        var result = await mediator.Send(
            new UpdatePropertyCommand(
                tenantContext.TenantId,
                request.FormId,
                request.PropertyId,
                request.Name,
                request.SortOrder),
            ct);

        return TypedResultsBuilder
            .MapResult(result, AudiencePropertyResponse.FromDto)
            .SetTypedResults<Ok<AudiencePropertyResponse>, ProblemHttpResult>();
    }
}

/// <summary>
/// Validator for update audience property.
/// </summary>
public sealed class UpdateAudiencePropertyValidator : Validator<UpdateAudiencePropertyRequest>
{
    public UpdateAudiencePropertyValidator()
    {
        RuleFor(request => request.FormId).GreaterThan(0);
        RuleFor(request => request.PropertyId).GreaterThan(0);
        RuleFor(request => request.Name)
            .MaximumLength(DataSchemaConstants.MAX_NAME_LENGTH)
            .Must(name => Property.NameError(name) is null)
            .WithMessage((_, name) => Property.NameError(name))
            .When(request => request.Name is not null);
        RuleFor(request => request)
            .Must(request => request.Name is not null || request.SortOrder is not null)
            .WithMessage("Provide Name and/or SortOrder.");
    }
}

/// <summary>
/// Request to update an audience property.
/// </summary>
public sealed class UpdateAudiencePropertyRequest
{
    public long FormId { get; init; }

    public long PropertyId { get; init; }

    public string? Name { get; init; }

    public int? SortOrder { get; init; }
}
