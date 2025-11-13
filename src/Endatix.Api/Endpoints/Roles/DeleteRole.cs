using FastEndpoints;
using MediatR;
using Microsoft.AspNetCore.Http.HttpResults;
using Endatix.Core.Abstractions.Authorization;
using Endatix.Core.UseCases.Identity.DeleteRole;
using Endatix.Api.Infrastructure;

namespace Endatix.Api.Endpoints.Roles;

/// <summary>
/// Endpoint for deleting a role (admin-only).
/// </summary>
public class DeleteRole(IMediator mediator)
    : Endpoint<DeleteRoleRequest, Results<Ok<DeleteRoleResponse>, ProblemHttpResult>>
{
    /// <summary>
    /// Configures the endpoint settings.
    /// </summary>
    public override void Configure()
    {
        Delete("roles/{roleName}");
        Permissions(Actions.Tenant.ManageRoles);
        Summary(s =>
        {
            s.Summary = "Delete a role";
            s.Description = "Deletes the specified role for the current tenant. Admin-only access.";
            s.Responses[200] = "Role deleted successfully.";
            s.Responses[400] = "Invalid request or role deletion failed.";
            s.Responses[404] = "Role not found.";
        });
    }

    /// <inheritdoc/>
    public override async Task<Results<Ok<DeleteRoleResponse>, ProblemHttpResult>> ExecuteAsync(
        DeleteRoleRequest request,
        CancellationToken cancellationToken)
    {
        var command = new DeleteRoleCommand(request.RoleName);
        var result = await mediator.Send(command, cancellationToken);

        return TypedResultsBuilder
            .MapResult(result, (message) => new DeleteRoleResponse(message))
            .SetTypedResults<Ok<DeleteRoleResponse>, ProblemHttpResult>();
    }
}
