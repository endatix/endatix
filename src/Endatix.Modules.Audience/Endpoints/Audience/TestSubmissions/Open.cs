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
/// Test as this person: opens a new test submission on the form's current definition with the person's current values.
/// </summary>
public sealed class Open(IMediator mediator, ITenantContext tenantContext)
    : Endpoint<OpenTestSubmissionRequest, Results<Created<TestSubmissionResponse>, ProblemHttpResult>>
{
    public override void Configure()
    {
        Post("forms/{formId}/audience/test-submissions");
        Permissions(Actions.Forms.Test);
        Summary(summary =>
        {
            summary.Summary = "Open a test submission as a person";
            summary.Description =
                "Creates a new test submission for the person on the form's current definition, with their current " +
                "values frozen as personalization (source test). Every call creates a new one. The person's " +
                "personalised link and Submitter are not touched.";
            summary.Responses[201] = "Test submission opened. The token opens it on the respondent page.";
            summary.Responses[400] = "Invalid input.";
            summary.Responses[404] = "Form not found or not enabled, or the person is not on this form.";
        });
        Description(builder => builder
            .Produces<TestSubmissionResponse>(StatusCodes.Status201Created, "application/json")
            .ProducesProblem(400)
            .ProducesProblem(404));
    }

    public override async Task<Results<Created<TestSubmissionResponse>, ProblemHttpResult>> ExecuteAsync(
        OpenTestSubmissionRequest request,
        CancellationToken ct)
    {
        var result = await mediator.Send(
            new OpenTestSubmissionCommand(tenantContext.TenantId, request.FormId, request.MembershipId),
            ct);
        return TypedResultsBuilder
            .MapResult(result, TestSubmissionResponse.From)
            .SetTypedResults<Created<TestSubmissionResponse>, ProblemHttpResult>();
    }
}

public sealed class OpenTestSubmissionValidator : Validator<OpenTestSubmissionRequest>
{
    public OpenTestSubmissionValidator()
    {
        RuleFor(request => request.FormId).GreaterThan(0);
        RuleFor(request => request.MembershipId).GreaterThan(0);
    }
}

public sealed class OpenTestSubmissionRequest
{
    public long FormId { get; init; }

    public long MembershipId { get; init; }
}

/// <summary>The new test submission and the token the respondent page opens it with.</summary>
public sealed class TestSubmissionResponse
{
    public long SubmissionId { get; init; }

    public string Token { get; init; } = string.Empty;

    public static TestSubmissionResponse From(OpenedTestSubmissionDto dto) => new()
    {
        SubmissionId = dto.SubmissionId,
        Token = dto.Token,
    };
}
