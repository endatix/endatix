using Endatix.Api.Infrastructure;
using Endatix.Core.Abstractions;
using Endatix.Core.Abstractions.Authorization;
using Endatix.Core.Common;
using Endatix.Modules.Audience.Features.People;
using FastEndpoints;
using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Endatix.Modules.Audience.Endpoints.Audience.People;

/// <summary>
/// Adds a person to a form's audience.
/// </summary>
public sealed class Create(
    IMediator mediator,
    ITenantContext tenantContext)
    : Endpoint<CreateAudiencePersonRequest, Results<Created<AudiencePersonResponse>, ProblemHttpResult>>
{
    public override void Configure()
    {
        Post("forms/{formId}/audience/people");
        Permissions(Actions.Forms.Edit);
        Summary(summary =>
        {
            summary.Summary = "Add audience person";
            summary.Description =
                "Adds a person to this form. Creates the tenant member when the identifier is new.";
            summary.Responses[201] = "Person added.";
            summary.Responses[400] = "Invalid input.";
            summary.Responses[404] = "Form not found.";
            summary.Responses[409] = "Person already on this form.";
        });
        Description(builder => builder
            .Produces<AudiencePersonResponse>(StatusCodes.Status201Created, "application/json")
            .ProducesProblem(400)
            .ProducesProblem(404)
            .ProducesProblem(409));
    }

    public override async Task<Results<Created<AudiencePersonResponse>, ProblemHttpResult>> ExecuteAsync(
        CreateAudiencePersonRequest request,
        CancellationToken ct)
    {
        var result = await mediator.Send(
            new CreatePersonCommand(
                tenantContext.TenantId,
                request.FormId,
                request.Identifier ?? string.Empty,
                request.Values),
            ct);

        return TypedResultsBuilder
            .MapResult(result, AudiencePersonResponse.FromDto)
            .SetTypedResults<Created<AudiencePersonResponse>, ProblemHttpResult>();
    }
}

/// <summary>
/// Validator for create audience person.
/// </summary>
public sealed class CreateAudiencePersonValidator : Validator<CreateAudiencePersonRequest>
{
    public CreateAudiencePersonValidator()
    {
        RuleFor(request => request.FormId).GreaterThan(0);
        RuleFor(request => request.Identifier)
            .NotEmpty()
            .MaximumLength(DataSchemaConstants.MAX_EMAIL_LENGTH);
    }
}

/// <summary>
/// Request to add an audience person.
/// </summary>
public sealed class CreateAudiencePersonRequest
{
    public long FormId { get; init; }

    public string? Identifier { get; init; }

    public Dictionary<long, string>? Values { get; init; }
}
