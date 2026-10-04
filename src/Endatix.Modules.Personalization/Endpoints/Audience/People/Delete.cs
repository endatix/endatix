using Endatix.Api.Infrastructure;
using Endatix.Core.Abstractions;
using Endatix.Core.Abstractions.Authorization;
using Endatix.Modules.Personalization.Features.People;
using FastEndpoints;
using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Endatix.Modules.Personalization.Endpoints.Audience.People;

/// <summary>
/// Removes a person from this form only.
/// </summary>
public sealed class Delete(
    IMediator mediator,
    ITenantContext tenantContext)
    : Endpoint<DeleteAudiencePersonRequest, Results<Ok<string>, ProblemHttpResult>>
{
    public override void Configure()
    {
        Delete("forms/{formId}/audience/people/{membershipId}");
        Permissions(Actions.Forms.Edit);
        Summary(summary =>
        {
            summary.Summary = "Remove audience person from form";
            summary.Description =
                "Removes the person from this form only. The tenant member and other forms stay.";
            summary.Responses[200] = "Person removed from this form.";
            summary.Responses[404] = "Form or membership not found.";
        });
        Description(builder => builder
            .Produces<string>(StatusCodes.Status200OK, "application/json")
            .ProducesProblem(404));
    }

    public override async Task<Results<Ok<string>, ProblemHttpResult>> ExecuteAsync(
        DeleteAudiencePersonRequest request,
        CancellationToken ct)
    {
        var result = await mediator.Send(
            new DeletePersonCommand(
                tenantContext.TenantId,
                request.FormId,
                request.MembershipId),
            ct);

        return TypedResultsBuilder
            .FromResult(result)
            .SetTypedResults<Ok<string>, ProblemHttpResult>();
    }
}

/// <summary>
/// Validator for delete audience person.
/// </summary>
public sealed class DeleteAudiencePersonValidator : Validator<DeleteAudiencePersonRequest>
{
    public DeleteAudiencePersonValidator()
    {
        RuleFor(request => request.FormId).GreaterThan(0);
        RuleFor(request => request.MembershipId).GreaterThan(0);
    }
}

/// <summary>
/// Request to remove an audience person from a form.
/// </summary>
public sealed class DeleteAudiencePersonRequest
{
    public long FormId { get; init; }

    public long MembershipId { get; init; }
}
