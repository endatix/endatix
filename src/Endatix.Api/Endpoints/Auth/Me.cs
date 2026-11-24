using Endatix.Api.Infrastructure;
using Endatix.Core.Abstractions.Authorization;
using Endatix.Core.Infrastructure.Result;
using FastEndpoints;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Endatix.Api.Endpoints.Auth;

/// <summary>
/// Endpoint for getting current user information including roles and permissions.
/// Used by clients (e.g., Next.js Hub) to fetch fresh permission data without re-authentication.
/// </summary>
public class Me(ICurrentUserAuthorizationService authorizationService)
    : EndpointWithoutRequest<Results<Ok<AuthorizationData>, UnauthorizedHttpResult, ProblemHttpResult>>
{
    public override void Configure()
    {
        Get("auth/me");
        Summary(s =>
        {
            s.Summary = "Get current user information";
            s.Description = "Returns current authenticated user information including roles, permissions, and tenant context. " +
                          "Permissions are resolved server-side for freshness and accuracy.";
            s.Responses[200] = "User information retrieved successfully.";
            s.Responses[401] = "Unauthorized - authentication required.";
            s.Responses[503] = "Required action status is temporarily unavailable.";
        });
    }

    public override async Task<Results<Ok<AuthorizationData>, UnauthorizedHttpResult, ProblemHttpResult>> ExecuteAsync(
        CancellationToken ct)
    {
        var authorizationDataResult = await authorizationService.GetAuthorizationDataAsync(ct);
        if (!authorizationDataResult.IsSuccess)
        {
            return TypedResults.Unauthorized();
        }

        if (authorizationDataResult.Value.RequiredActionsUnavailable)
        {
            return Result.Unavailable("Required action status is unavailable. Try again.").ToProblem();
        }

        return TypedResults.Ok(authorizationDataResult.Value);
    }
}
