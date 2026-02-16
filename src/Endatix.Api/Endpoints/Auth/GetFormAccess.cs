using FastEndpoints;
using Microsoft.AspNetCore.Http.HttpResults;
using Endatix.Core.Abstractions.Submissions;
using Endatix.Api.Infrastructure;

namespace Endatix.Api.Endpoints.Auth;

/// <summary>
/// Endpoint for getting form access permissions.
/// Returns simplified flat permission arrays for O(1) client-side access.
/// Identity (who the user is) should be fetched from /auth/me endpoint.
/// </summary>
public class GetFormAccess(
    ISubmissionAccessControl submissionAccessControl
) : Endpoint<GetFormAccessRequest, Results<Ok<FormAccessData>, ProblemHttpResult>>
{
    /// <summary>
    /// Configures the endpoint settings.
    /// </summary>
    public override void Configure()
    {
        Get("auth/access/form");
        AllowAnonymous();
        Summary(s =>
        {
            s.Summary = "Get form access permissions";
            s.Description = "Gets computed permissions for a form and its submissions based on authorization context (admin, token, user, or public).";
            s.Responses[200] = "Permissions retrieved successfully.";
            s.Responses[400] = "Invalid input data.";
            s.Responses[403] = "Access denied.";
            s.Responses[404] = "Form not found.";
        });
    }

    /// <inheritdoc/>
    public override async Task<Results<Ok<FormAccessData>, ProblemHttpResult>> ExecuteAsync(
        GetFormAccessRequest request,
        CancellationToken ct)
    {
        var accessDataResult = await submissionAccessControl.GetAccessDataAsync(
            new SubmissionAccessContext(request.FormId, request.SubmissionId, request.Token),
            ct);


        return TypedResultsBuilder
            .FromResult(accessDataResult)
            .SetTypedResults<Ok<FormAccessData>, ProblemHttpResult>();
    }
}
