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
/// Reads one participant's values as a preset, for Preview as.
/// </summary>
public sealed class GetParticipantPreset(
    IMediator mediator,
    ITenantContext tenantContext)
    : Endpoint<GetParticipantPresetRequest, Results<Ok<AudienceVariablePresetResponse>, ProblemHttpResult>>
{
    /// <inheritdoc />
    public override void Configure()
    {
        Get("forms/{formId}/audience/variables/participants/{memberId}");
        Permissions(Actions.Forms.Edit);
        Summary(summary =>
        {
            summary.Summary = "Get a participant's audience variables";
            summary.Description =
                "Reads one participant on the form, real or test, as a SurveyJS variable preset: name is their " +
                "identifier and variables their values typed as the respondent's personalization, as in the variables " +
                "read.";
            summary.Responses[200] = "The participant's preset.";
            summary.Responses[400] = "Invalid form or member id.";
            summary.Responses[404] = "Form not found, or the participant is not on it.";
        });
        Description(builder => builder
            .Produces<AudienceVariablePresetResponse>(200, "application/json")
            .ProducesProblem(400)
            .ProducesProblem(404));
    }

    /// <inheritdoc />
    public override async Task<Results<Ok<AudienceVariablePresetResponse>, ProblemHttpResult>> ExecuteAsync(
        GetParticipantPresetRequest request,
        CancellationToken ct)
    {
        var result = await mediator.Send(
            new GetParticipantPresetQuery(tenantContext.TenantId, request.FormId, request.MemberId),
            ct);

        return TypedResultsBuilder
            .MapResult(result, AudienceVariablePresetResponse.From)
            .SetTypedResults<Ok<AudienceVariablePresetResponse>, ProblemHttpResult>();
    }
}

/// <summary>
/// Validator for get a participant's audience variables.
/// </summary>
public sealed class GetParticipantPresetValidator : Validator<GetParticipantPresetRequest>
{
    /// <summary>The form and member ids are positive.</summary>
    public GetParticipantPresetValidator()
    {
        RuleFor(request => request.FormId).GreaterThan(0);
        RuleFor(request => request.MemberId).GreaterThan(0);
    }
}

/// <summary>
/// Request to read one participant's audience variables.
/// </summary>
public sealed class GetParticipantPresetRequest
{
    /// <summary>The form.</summary>
    public long FormId { get; init; }

    /// <summary>The audience member id (<c>audienceMemberId</c> on the people list), not the membership id.</summary>
    public long MemberId { get; init; }
}
