using FastEndpoints;
using MediatR;
using Microsoft.AspNetCore.Http.HttpResults;
using Endatix.Core.Abstractions.Authorization;
using Endatix.Core.UseCases.Identity.RemoveRole;
using Endatix.Api.Infrastructure;

namespace Endatix.Api.Endpoints.Users;

/// <summary>
/// Endpoint for removing a role from a user (admin-only).
/// </summary>
public class RemoveRoleFromUser(IMediator mediator)
    : Endpoint<RemoveRoleRequest, Results<Ok<RemoveRoleResponse>, ProblemHttpResult>>
{
    /// <summary>
    /// Configures the endpoint settings.
    /// </summary>
    public override void Configure()
    {
        Delete("users/{userId}/roles/{roleName}");
        Permissions(Actions.Tenant.ManageUsers);
        Summary(s =>
        {
            s.Summary = "Remove a role from a user";
            s.Description = "Removes the specified role from a user. Admin-only access.";
            s.Responses[200] = "Role removed successfully.";
            s.Responses[400] = "Invalid request or role removal failed.";
            s.Responses[404] = "User not found.";
        });
    }

    /// <inheritdoc/>
    public override async Task<Results<Ok<RemoveRoleResponse>, ProblemHttpResult>> ExecuteAsync(
        RemoveRoleRequest request,
        CancellationToken cancellationToken)
    {
        var command = new RemoveRoleCommand(request.UserId, request.RoleName);
        var result = await mediator.Send(command, cancellationToken);

        return TypedResultsBuilder
            .MapResult(result, (message) => new RemoveRoleResponse(message))
            .SetTypedResults<Ok<RemoveRoleResponse>, ProblemHttpResult>();
    }
}
