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
    public override void Configure()
    {
        Post("forms/{formId}/audience/test-submissions/{submissionId}/restart");
        Permissions(Actions.Forms.Test);
        Summary(summary =>
        {
            summary.Summary = "Start over a test submission";
            summary.Description =
                "Deletes the test submission and opens a new one for the same person, on the form's current " +
                "definition and the person's current values. Only a test submission opened as a person can start over.";
            summary.Responses[201] = "New test submission opened. The token opens it on the respondent page.";
            summary.Responses[400] = "Not a test submission opened as a person.";
            summary.Responses[404] = "Form, submission or person not found.";
        });
        Description(builder => builder
            .Produces<TestSubmissionResponse>(StatusCodes.Status201Created, "application/json")
            .ProducesProblem(400)
            .ProducesProblem(404));
    }

    public override async Task<Results<Created<TestSubmissionResponse>, ProblemHttpResult>> ExecuteAsync(
        RestartTestSubmissionRequest request,
        CancellationToken ct)
    {
        var result = await mediator.Send(
            new RestartTestSubmissionCommand(tenantContext.TenantId, request.FormId, request.SubmissionId),
            ct);
        return TypedResultsBuilder
            .MapResult(result, TestSubmissionResponse.From)
            .SetTypedResults<Created<TestSubmissionResponse>, ProblemHttpResult>();
    }
}

public sealed class RestartTestSubmissionValidator : Validator<RestartTestSubmissionRequest>
{
    public RestartTestSubmissionValidator()
    {
        RuleFor(request => request.FormId).GreaterThan(0);
        RuleFor(request => request.SubmissionId).GreaterThan(0);
    }
}

public sealed class RestartTestSubmissionRequest
{
    public long FormId { get; init; }

    public long SubmissionId { get; init; }
}
