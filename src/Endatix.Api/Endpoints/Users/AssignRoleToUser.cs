using FastEndpoints;
using MediatR;
using Microsoft.AspNetCore.Http.HttpResults;
using Endatix.Core.Abstractions.Authorization;
using Endatix.Core.UseCases.Identity.AssignRole;
using Endatix.Api.Infrastructure;

namespace Endatix.Api.Endpoints.Users;

/// <summary>
/// Endpoint for assigning a role to a user (admin-only).
/// </summary>
public class AssignRoleToUser(IMediator mediator)
    : Endpoint<AssignRoleRequest, Results<Ok<AssignRoleResponse>, ProblemHttpResult>>
{
    /// <summary>
    /// Configures the endpoint settings.
    /// </summary>
    public override void Configure()
    {
        Post("users/{userId}/roles");
        Permissions(Actions.Tenant.ManageUsers);
        Summary(s =>
        {
            s.Summary = "Assign a role to a user";
            s.Description = "Assigns the specified role to a user. Admin-only access.";
            s.Responses[200] = "Role assigned successfully.";
            s.Responses[400] = "Invalid request or role assignment failed.";
            s.Responses[404] = "User not found.";
        });
    }

    /// <inheritdoc/>
    public override async Task<Results<Ok<AssignRoleResponse>, ProblemHttpResult>> ExecuteAsync(
        AssignRoleRequest request,
        CancellationToken cancellationToken)
    {
        var command = new AssignRoleCommand(request.UserId, request.RoleName);
        var result = await mediator.Send(command, cancellationToken);

        return TypedResultsBuilder
            .MapResult(result, (message) => new AssignRoleResponse(message))
            .SetTypedResults<Ok<AssignRoleResponse>, ProblemHttpResult>();
    }
}
