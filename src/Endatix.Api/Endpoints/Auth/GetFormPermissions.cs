using FastEndpoints;
using Microsoft.AspNetCore.Http.HttpResults;
using Endatix.Core.Abstractions.Submissions;
using Endatix.Api.Infrastructure;

namespace Endatix.Api.Endpoints.Auth;

/// <summary>
/// Endpoint for getting form permissions.
/// </summary>
public class GetFormPermissions(
    ISubmissionAccessControl submissionAccessControl
) : Endpoint<GetFormPermissionsRequest, Results<Ok<ResourceAccessData>, ProblemHttpResult>>
{
    /// <summary>
    /// Configures the endpoint settings.
    /// </summary>
    public override void Configure()
    {
        Get("auth/permissions/submission");
        AllowAnonymous();
        Summary(s =>
        {
            s.Summary = "Get submission permissions";
            s.Description = "Gets computed permissions for a submission based on authorization context (admin, token, user, or public).";
            s.Responses[200] = "Permissions retrieved successfully.";
            s.Responses[400] = "Invalid input data.";
            s.Responses[403] = "Access denied.";
            s.Responses[404] = "Form not found.";
        });
    }

    /// <inheritdoc/>
    public override async Task<Results<Ok<ResourceAccessData>, ProblemHttpResult>> ExecuteAsync(
        GetFormPermissionsRequest request,
        CancellationToken cancellationToken)
    {
        var accessDataResult = await submissionAccessControl.GetAccessDataAsync(
            new SubmissionAccessContext(request.FormId, request.SubmissionId, request.Token),
            cancellationToken);


        return TypedResultsBuilder
            .FromResult(accessDataResult)
            .SetTypedResults<Ok<ResourceAccessData>, ProblemHttpResult>();
    }
}
