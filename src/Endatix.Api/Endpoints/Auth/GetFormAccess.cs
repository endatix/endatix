using Endatix.Api.Infrastructure;
using Endatix.Core.Abstractions.Submissions;
using Endatix.Core.Authorization;
using Endatix.Core.Authorization.Models;
using FastEndpoints;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Endatix.Api.Endpoints.Auth;

/// <summary>
/// Public endpoint for ReBAC (resource based access control) for form/submission related access control (JWT or access token based) on public pages that are used for submission, prefilling and forms sharing.
/// Use FormId + Token + TokenType (no SubmissionId; submission is resolved from token).
/// Returns the permissions for the form and its submissions in public/token context (no auth).
/// </summary>
public class GetFormAccess(
    IResourceAccessStrategy<SubmissionAccessData, SubmissionAccessContext> submissionAccessPolicy
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
