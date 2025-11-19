using FastEndpoints;
using MediatR;
using Microsoft.AspNetCore.Http.HttpResults;
using Endatix.Core.Abstractions.Authorization;
using Endatix.Core.UseCases.Identity.GetUserRoles;
using Endatix.Api.Infrastructure;

namespace Endatix.Api.Endpoints.Users;

/// <summary>
/// Endpoint for retrieving roles assigned to a user (admin-only).
/// </summary>
public class GetUserRoles(IMediator mediator)
    : Endpoint<GetUserRolesRequest, Results<Ok<IList<string>>, ProblemHttpResult>>
{
    /// <summary>
    /// Configures the endpoint settings.
    /// </summary>
    public override void Configure()
    {
        Get("users/{userId}/roles");
        Permissions(Actions.Tenant.ViewUsers);
        Summary(s =>
        {
            s.Summary = "Get roles assigned to a user";
            s.Description = "Retrieves all roles assigned to the specified user. Admin-only access.";
            s.Responses[200] = "Roles retrieved successfully.";
            s.Responses[404] = "User not found.";
        });
    }

    /// <inheritdoc/>
    public override async Task<Results<Ok<IList<string>>, ProblemHttpResult>> ExecuteAsync(
        GetUserRolesRequest request,
        CancellationToken cancellationToken)
    {
        var query = new GetUserRolesQuery(request.UserId);
        var result = await mediator.Send(query, cancellationToken);

        return TypedResultsBuilder
            .FromResult(result)
            .SetTypedResults<Ok<IList<string>>, ProblemHttpResult>();
    }
}
