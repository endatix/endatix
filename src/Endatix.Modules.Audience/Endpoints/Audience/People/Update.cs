using Endatix.Api.Infrastructure;
using Endatix.Core.Abstractions;
using Endatix.Core.Abstractions.Authorization;
using Endatix.Modules.Audience.Features.People;
using FastEndpoints;
using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Endatix.Modules.Audience.Endpoints.Audience.People;

/// <summary>
/// Updates property values for a person on this form.
/// </summary>
public sealed class Update(
    IMediator mediator,
    ITenantContext tenantContext)
    : Endpoint<UpdateAudiencePersonRequest, Results<Ok<AudiencePersonResponse>, ProblemHttpResult>>
{
    public override void Configure()
    {
        Put("forms/{formId}/audience/people/{membershipId}");
        Permissions(Actions.Forms.Edit);
        Summary(summary =>
        {
            summary.Summary = "Update audience person";
            summary.Description = "Updates property values for a person on this form.";
            summary.Responses[200] = "Person updated.";
            summary.Responses[404] = "Form or membership not found.";
        });
        Description(builder => builder
            .Produces<AudiencePersonResponse>(200, "application/json")
            .ProducesProblem(400)
            .ProducesProblem(404));
    }

    public override async Task<Results<Ok<AudiencePersonResponse>, ProblemHttpResult>> ExecuteAsync(
        UpdateAudiencePersonRequest request,
        CancellationToken ct)
    {
        var result = await mediator.Send(
            new UpdatePersonCommand(
                tenantContext.TenantId,
                request.FormId,
                request.MembershipId,
                request.Values ?? new Dictionary<long, string>()),
            ct);

        return TypedResultsBuilder
            .MapResult(result, AudiencePersonResponse.FromDto)
            .SetTypedResults<Ok<AudiencePersonResponse>, ProblemHttpResult>();
    }
}

/// <summary>
/// Validator for update audience person.
/// </summary>
public sealed class UpdateAudiencePersonValidator : Validator<UpdateAudiencePersonRequest>
{
    public UpdateAudiencePersonValidator()
    {
        RuleFor(request => request.FormId).GreaterThan(0);
        RuleFor(request => request.MembershipId).GreaterThan(0);
        RuleFor(request => request.Values).NotNull();
    }
}

/// <summary>
/// Request to update an audience person.
/// </summary>
public sealed class UpdateAudiencePersonRequest
{
    public long FormId { get; init; }

    public long MembershipId { get; init; }

    public Dictionary<long, string>? Values { get; init; }
}
