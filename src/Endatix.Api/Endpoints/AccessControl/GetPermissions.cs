using FastEndpoints;
using Microsoft.AspNetCore.Http.HttpResults;
using Endatix.Api.Infrastructure;
using Endatix.Core.Abstractions.Submissions;
using Endatix.Core.Authorization.Models;
using Endatix.Infrastructure.Features.AccessControl;

namespace Endatix.Api.Endpoints.AccessControl;

/// <summary>
/// Management endpoint for form/submission permissions (authenticated, RBAC).
/// </summary>
public class GetPermissions(
    SubmissionManagementAccessPolicy managementAccessPolicy
) : Endpoint<GetPermissionsRequest, Results<Ok<GetPermissionsResponse>, ProblemHttpResult>>
{
    public override void Configure()
    {
        Get("access-control/permissions");
        Summary(s =>
        {
            s.Summary = "Get permissions (management)";
            s.Description = "Gets RBAC permissions for a form and optional submission for the current user.";
            s.Responses[200] = "Permissions retrieved successfully.";
            s.Responses[400] = "Invalid input data.";
            s.Responses[401] = "Unauthorized.";
            s.Responses[404] = "Form not found.";
        });
    }

    public override async Task<Results<Ok<GetPermissionsResponse>, ProblemHttpResult>> ExecuteAsync(
        GetPermissionsRequest request,
        CancellationToken ct)
    {
        var context = new SubmissionAccessContext(request.FormId, token: null, tokenType: null);
        var accessDataResult = await managementAccessPolicy.GetAccessData(context, ct);

        return TypedResultsBuilder
            .MapResult(accessDataResult, GetPermissionsResponse.FromCached)
            .SetTypedResults<Ok<GetPermissionsResponse>, ProblemHttpResult>();
    }
}
