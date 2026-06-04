using Endatix.Api.Infrastructure;
using Endatix.Core.Abstractions.Authorization;
using Endatix.Core.UseCases.Identity.UpdateRole;
using FastEndpoints;
using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Endatix.Api.Endpoints.Roles;

/// <summary>
/// Endpoint for updating an existing role's description and permissions.
/// </summary>
public sealed class UpdateRole(IMediator mediator)
    : Endpoint<UpdateRoleRequest, Results<Ok<UpdateRoleResponse>, ProblemHttpResult>>
{
    /// <summary>
    /// Configures the endpoint settings.
    /// </summary>
    public override void Configure()
    {
        Put("roles/{roleName}");
        Permissions(Actions.Tenant.ManageRoles);
        Summary(s =>
        {
            s.Summary = "Update a role";
            s.Description = "Updates an existing role's description and permission assignments.";
            s.ExampleRequest = new UpdateRoleRequest
            {
                RoleName = "Editor",
                Description = "Can edit forms and view submissions",
                Permissions = ["forms.edit", "submissions.view"]
            };
            s.Responses[200] = "Role updated successfully.";
            s.Responses[400] = "Invalid input data.";
            s.Responses[404] = "Role not found.";
        });
        Description(builder => builder
            .Produces<UpdateRoleResponse>(200, "application/json")
            .ProducesProblem(400)
            .ProducesProblem(404));
    }

    /// <inheritdoc/>
    public override async Task<Results<Ok<UpdateRoleResponse>, ProblemHttpResult>> ExecuteAsync(
        UpdateRoleRequest request,
        CancellationToken cancellationToken)
    {
        var result = await mediator.Send(
            new UpdateRoleCommand(request.RoleName!, request.Description, request.Permissions),
            cancellationToken);

        return TypedResultsBuilder
            .MapResult(result, roleId => new UpdateRoleResponse(roleId))
            .SetTypedResults<Ok<UpdateRoleResponse>, ProblemHttpResult>();
    }
}

/// <summary>
/// Request to update a role.
/// </summary>
public sealed record UpdateRoleRequest
{
    /// <summary>
    /// The name of the role to update (from route).
    /// </summary>
    public string? RoleName { get; init; }

    /// <summary>
    /// An optional description of the role.
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    /// The list of permission names to assign to the role.
    /// </summary>
    public List<string> Permissions { get; init; } = [];
}

/// <summary>
/// Response returned after updating a role.
/// </summary>
/// <param name="Id">The role identifier.</param>
public sealed record UpdateRoleResponse(string Id);

/// <summary>
/// Validator for the <see cref="UpdateRoleRequest"/> class.
/// </summary>
public sealed class UpdateRoleValidator : Validator<UpdateRoleRequest>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="UpdateRoleValidator"/> class.
    /// </summary>
    public UpdateRoleValidator()
    {
        RuleFor(x => x.RoleName)
            .NotEmpty()
            .MaximumLength(256);

        RuleFor(x => x.Description)
            .MaximumLength(500)
            .When(x => !string.IsNullOrEmpty(x.Description));

        RuleFor(x => x.Permissions).NotNull();
        RuleForEach(x => x.Permissions).NotEmpty();
    }
}
