using FastEndpoints;
using Microsoft.AspNetCore.Http.HttpResults;
using Endatix.Api.Infrastructure;
using Endatix.Core.Abstractions.Submissions;
using Endatix.Infrastructure.Features.AccessControl;

namespace Endatix.Api.Endpoints.Auth;

/// <summary>
/// Public endpoint for form/submission access (anonymous or token-based).
/// Returns permissions for public forms and valid submission tokens only.
/// Use FormId + Token + TokenType (no SubmissionId; submission is resolved from token).
/// </summary>
public class GetFormAccess(
    SubmissionAccessPolicy submissionAccessPolicy
) : Endpoint<GetFormAccessRequest, Results<Ok<GetFormAccessResponse>, ProblemHttpResult>>
{
    public override void Configure()
    {
        Get("auth/access/form/{formId}");
        AllowAnonymous();
        Summary(s =>
        {
            s.Summary = "Get public form access";
            s.Description = "Gets permissions for a form and its submissions in public/token context (no auth).";
            s.Responses[200] = "Permissions retrieved successfully.";
            s.Responses[400] = "Invalid input data.";
            s.Responses[404] = "Form not found.";
        });
    }

    public override async Task<Results<Ok<GetFormAccessResponse>, ProblemHttpResult>> ExecuteAsync(
        GetFormAccessRequest request,
        CancellationToken ct)
    {
        var context = new SubmissionAccessContext(request.FormId, request.Token, request.TokenType);
        var accessDataResult = await submissionAccessPolicy.GetAccessData(context, ct);

        return TypedResultsBuilder
            .MapResult(accessDataResult, GetFormAccessResponse.FromCached)
            .SetTypedResults<Ok<GetFormAccessResponse>, ProblemHttpResult>();
    }
}
