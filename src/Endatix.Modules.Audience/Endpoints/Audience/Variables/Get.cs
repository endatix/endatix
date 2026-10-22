using Endatix.Api.Infrastructure;
using Endatix.Core.Abstractions;
using Endatix.Core.Abstractions.Authorization;
using Endatix.Modules.Audience.Features.Variables;
using FastEndpoints;
using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Endatix.Modules.Audience.Endpoints.Audience.Variables;

/// <summary>
/// Reads this form's Audience variables for the form designer.
/// </summary>
public sealed class Get(
    IMediator mediator,
    ITenantContext tenantContext)
    : Endpoint<GetAudienceVariablesRequest, Results<Ok<AudienceVariablesResponse>, ProblemHttpResult>>
{
    /// <inheritdoc />
    public override void Configure()
    {
        Get("forms/{formId}/audience/variables");
        Permissions(Actions.Forms.Edit);
        Summary(summary =>
        {
            summary.Summary = "Get audience variables";
            summary.Description =
                "Reads the form's Properties as a SurveyJS variable definition, one preset per test participant named by " +
                "identifier with values typed as the respondent's personalization, coverage and the preset editing " +
                "policy. Presets cover the first 20 test participants added; totalPresets counts them all. " +
                "variablePresets is null when the form has no Properties.";
            summary.Responses[200] = "The form's variables.";
            summary.Responses[400] = "Invalid form id.";
            summary.Responses[404] = "Form not found.";
        });
        Description(builder => builder
            .Produces<AudienceVariablesResponse>(200, "application/json")
            .ProducesProblem(400)
            .ProducesProblem(404));
    }

    /// <inheritdoc />
    public override async Task<Results<Ok<AudienceVariablesResponse>, ProblemHttpResult>> ExecuteAsync(
        GetAudienceVariablesRequest request,
        CancellationToken ct)
    {
        var result = await mediator.Send(new GetAudienceVariablesQuery(tenantContext.TenantId, request.FormId), ct);

        return TypedResultsBuilder
            .MapResult(result, AudienceVariablesResponse.FromDto)
            .SetTypedResults<Ok<AudienceVariablesResponse>, ProblemHttpResult>();
    }
}

/// <summary>
/// Validator for get audience variables.
/// </summary>
public sealed class GetAudienceVariablesValidator : Validator<GetAudienceVariablesRequest>
{
    /// <summary>The form id is positive.</summary>
    public GetAudienceVariablesValidator()
    {
        RuleFor(request => request.FormId).GreaterThan(0);
    }
}

/// <summary>
/// Request to read a form's audience variables.
/// </summary>
public sealed class GetAudienceVariablesRequest
{
    /// <summary>The form.</summary>
    public long FormId { get; init; }
}
