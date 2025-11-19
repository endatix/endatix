using FastEndpoints;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Http;
using Endatix.Core.Abstractions.Authorization;

namespace Endatix.Api.Endpoints.Auth;

/// <summary>
/// Endpoint for getting current user information including roles and permissions.
/// Used by clients (e.g., Next.js Hub) to fetch fresh permission data without re-authentication.
/// </summary>
public class Me(ICurrentUserAuthorizationService authorizationService)
    : EndpointWithoutRequest<Results<Ok<AuthorizationData>, UnauthorizedHttpResult>>
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
        });
    }

    public override async Task<Results<Ok<AuthorizationData>, UnauthorizedHttpResult>> ExecuteAsync(
        CancellationToken cancellationToken)
    {
        // Get user roles and permissions from AuthorizationService
        var authorizationDataResult = await authorizationService.GetAuthorizationDataAsync(cancellationToken);
        if (!authorizationDataResult.IsSuccess)
        {
            return TypedResults.Unauthorized();
        }


        return TypedResults.Ok(authorizationDataResult.Value);
    }
}
