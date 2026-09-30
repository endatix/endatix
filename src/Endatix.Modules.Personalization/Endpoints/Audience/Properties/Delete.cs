using Endatix.Api.Infrastructure;
using Endatix.Core.Abstractions;
using Endatix.Core.Abstractions.Authorization;
using Endatix.Modules.Personalization.Features.Properties;
using FastEndpoints;
using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Endatix.Modules.Personalization.Endpoints.Audience.Properties;

/// <summary>
/// Soft-deletes an audience property and its value cells.
/// </summary>
public sealed class Delete(
    IMediator mediator,
    ITenantContext tenantContext)
    : Endpoint<DeleteAudiencePropertyRequest, Results<Ok<string>, ProblemHttpResult>>
{
    public override void Configure()
    {
        Delete("forms/{formId}/audience/properties/{propertyId}");
        Permissions(Actions.Forms.Edit);
        Summary(summary =>
        {
            summary.Summary = "Delete audience property";
            summary.Description = "Soft-deletes a property and its values on this form.";
            summary.Responses[200] = "Property deleted.";
            summary.Responses[404] = "Form or property not found.";
        });
        Description(builder => builder
            .Produces<string>(StatusCodes.Status200OK, "application/json")
            .ProducesProblem(404));
    }

    public override async Task<Results<Ok<string>, ProblemHttpResult>> ExecuteAsync(
        DeleteAudiencePropertyRequest request,
        CancellationToken ct)
    {
        var result = await mediator.Send(
            new DeleteAudiencePropertyCommand(
                tenantContext.TenantId,
                request.FormId,
                request.PropertyId),
            ct);

        return TypedResultsBuilder
            .FromResult(result)
            .SetTypedResults<Ok<string>, ProblemHttpResult>();
    }
}

/// <summary>
/// Validator for delete audience property.
/// </summary>
public sealed class DeleteAudiencePropertyValidator : Validator<DeleteAudiencePropertyRequest>
{
    public DeleteAudiencePropertyValidator()
    {
        RuleFor(request => request.FormId).GreaterThan(0);
        RuleFor(request => request.PropertyId).GreaterThan(0);
    }
}

/// <summary>
/// Request to delete an audience property.
/// </summary>
public sealed class DeleteAudiencePropertyRequest
{
    public long FormId { get; init; }

    public long PropertyId { get; init; }
}
