using Endatix.Api.Infrastructure;
using Endatix.Core.Abstractions;
using Endatix.Core.Abstractions.Authorization;
using Endatix.Modules.Audience.Features.TestSubmissions;
using FastEndpoints;
using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Endatix.Modules.Audience.Endpoints.Audience.TestSubmissions;

/// <summary>
/// Start over: deletes a test submission opened as a person and opens a new one for them.
/// </summary>
public sealed class Restart(IMediator mediator, ITenantContext tenantContext)
    : Endpoint<RestartTestSubmissionRequest, Results<Created<TestSubmissionResponse>, ProblemHttpResult>>
{
    /// <inheritdoc />
    public override void Configure()
    {
        Post("forms/{formId}/audience/test-submissions/{submissionId}/restart");
        // forms.edit too: the submission carries the person's identifier and values, which every other Audience
        // route shows only to forms.edit.
        PermissionsAll(Actions.Forms.Test, Actions.Forms.Edit);
        Summary(summary =>
        {
            summary.Summary = "Start over a test submission";
            summary.Description =
                "Deletes the test submission and opens a new one for the same person, on the form's current " +
                "definition and the person's current values. Only a test submission opened as a person can start over, " +
                "and only with its token.";
            summary.Responses[201] = "New test submission opened. The token opens it on the respondent page.";
            summary.Responses[400] = "Not a test submission opened as a person.";
            summary.Responses[404] = "Form, submission or person not found, or the token is not the submission's.";
        });
        Description(builder => builder
            .Produces<TestSubmissionResponse>(StatusCodes.Status201Created, "application/json")
            .ProducesProblem(400)
            .ProducesProblem(404));
    }

    /// <inheritdoc />
    public override async Task<Results<Created<TestSubmissionResponse>, ProblemHttpResult>> ExecuteAsync(
        RestartTestSubmissionRequest request,
        CancellationToken ct)
    {
        var result = await mediator.Send(
            new RestartTestSubmissionCommand(tenantContext.TenantId, request.FormId, request.SubmissionId, request.Token),
            ct);
        return TypedResultsBuilder
            .MapResult(result, TestSubmissionResponse.From)
            .SetTypedResults<Created<TestSubmissionResponse>, ProblemHttpResult>();
    }
}

/// <summary>Validates <see cref="RestartTestSubmissionRequest"/>.</summary>
public sealed class RestartTestSubmissionValidator : Validator<RestartTestSubmissionRequest>
{
    /// <summary>Requires a form, a submission id and the submission's token.</summary>
    public RestartTestSubmissionValidator()
    {
        RuleFor(request => request.FormId).GreaterThan(0);
        RuleFor(request => request.SubmissionId).GreaterThan(0);
        RuleFor(request => request.Token).NotEmpty();
    }
}

/// <summary>Request to start over a test submission.</summary>
public sealed class RestartTestSubmissionRequest
{
    /// <summary>The form the test submission belongs to.</summary>
    public long FormId { get; init; }

    /// <summary>The test submission to discard.</summary>
    public long SubmissionId { get; init; }

    /// <summary>The token the test submission was opened with.</summary>
    public string Token { get; init; } = string.Empty;
}
