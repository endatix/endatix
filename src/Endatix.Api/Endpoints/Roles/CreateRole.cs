using FastEndpoints;
using MediatR;
using Microsoft.AspNetCore.Http.HttpResults;
using Endatix.Core.Abstractions.Authorization;
using Endatix.Core.UseCases.Identity.CreateRole;
using Endatix.Api.Infrastructure;

namespace Endatix.Api.Endpoints.Roles;

/// <summary>
/// Endpoint for creating a new role with permissions (admin-only).
/// </summary>
public class CreateRole(IMediator mediator)
    : Endpoint<CreateRoleRequest, Results<Created<CreateRoleResponse>, ProblemHttpResult>>
{
    /// <summary>
    /// Configures the endpoint settings.
    /// </summary>
    public override void Configure()
    {
        Post("roles");
        Permissions(Actions.Tenant.ManageRoles);
        Summary(s =>
        {
            s.Summary = "Create a new role with permissions";
            s.Description = "Creates a new role for the current tenant with the specified permissions. Admin-only access.";
            s.Responses[201] = "Role created successfully.";
            s.Responses[400] = "Invalid request or role creation failed.";
        });
    }

    /// <inheritdoc/>
    public override async Task<Results<Created<CreateRoleResponse>, ProblemHttpResult>> ExecuteAsync(
        CreateRoleRequest request,
        CancellationToken cancellationToken)
    {
        var command = new CreateRoleCommand(request.Name!, request.Description, request.Permissions);
        var result = await mediator.Send(command, cancellationToken);

        return TypedResultsBuilder
            .MapResult(result, (message) => new CreateRoleResponse(message))
            .SetTypedResults<Created<CreateRoleResponse>, ProblemHttpResult>();
    }
}
