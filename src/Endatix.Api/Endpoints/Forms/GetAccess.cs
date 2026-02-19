using FastEndpoints;
using Microsoft.AspNetCore.Http.HttpResults;
using Endatix.Api.Infrastructure;
using Endatix.Core.Abstractions.Submissions;
using Endatix.Infrastructure.Features.AccessControl;

namespace Endatix.Api.Endpoints.Forms;

/// <summary>
/// Public endpoint for form/submission access (anonymous or token-based).
/// Returns permissions for public forms and valid submission tokens only.
/// </summary>
public class GetAccess(
    SubmissionPublicAccessPolicy publicAccessPolicy
) : Endpoint<GetAccessRequest, Results<Ok<GetAccessResponse>, ProblemHttpResult>>
{
    public override void Configure()
    {
        Get("forms/{formId}/access");
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

    public override async Task<Results<Ok<GetAccessResponse>, ProblemHttpResult>> ExecuteAsync(
        GetAccessRequest request,
        CancellationToken ct)
    {
        var context = new SubmissionAccessContext(request.FormId, request.SubmissionId, request.Token);
        var accessDataResult = await publicAccessPolicy.GetAccessData(context, ct);

        return TypedResultsBuilder
            .MapResult(accessDataResult, GetAccessResponse.FromCached)
            .SetTypedResults<Ok<GetAccessResponse>, ProblemHttpResult>();
    }
}
