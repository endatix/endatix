using Endatix.Api.Infrastructure;
using Endatix.Core.Abstractions.Authorization;
using Endatix.Core.Entities.Identity;
using Endatix.Core.UseCases.Identity.ListRoles;
using FastEndpoints;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Endatix.Api.Endpoints.Permissions;

/// <summary>
/// Endpoint for listing all available permissions in the system.
/// </summary>
public sealed class ListPermissions(IMediator mediator)
    : EndpointWithoutRequest<Results<Ok<IEnumerable<ListPermissionsResponse>>, ProblemHttpResult>>
{
    /// <summary>
    /// Configures the endpoint settings.
    /// </summary>
    public override void Configure()
    {
        Get("permissions");
        Permissions(Actions.Tenant.ViewRoles, Actions.Tenant.ManageRoles);
        Summary(s =>
        {
            s.Summary = "List all permissions";
            s.Description = "Retrieves all available permissions in the system, including name, description, category, and whether each is system-defined.";
            s.Responses[200] = "List of permissions returned successfully.";
            s.Responses[400] = "Invalid request.";
        });
        Description(builder => builder
            .Produces<IEnumerable<ListPermissionsResponse>>(200, "application/json")
            .ProducesProblem(400));
    }

    /// <inheritdoc/>
    public override async Task<Results<Ok<IEnumerable<ListPermissionsResponse>>, ProblemHttpResult>> ExecuteAsync(CancellationToken ct)
    {
        var result = await mediator.Send(new ListPermissionsQuery(), ct);

        return TypedResultsBuilder
            .MapResult(result, Map)
            .SetTypedResults<Ok<IEnumerable<ListPermissionsResponse>>, ProblemHttpResult>();
    }

    private static IEnumerable<ListPermissionsResponse> Map(IReadOnlyList<PermissionListItem> permissions)
        => permissions.Select(permission => new ListPermissionsResponse
        {
            Id = permission.Id,
            Name = permission.Name,
            Description = permission.Description,
            Category = permission.Category,
            IsSystemDefined = permission.IsSystemDefined
        });
}

/// <summary>
/// Response containing a single permission entry.
/// </summary>
public sealed record ListPermissionsResponse
{
    /// <summary>
    /// The permission identifier.
    /// </summary>
    public long Id { get; init; }

    /// <summary>
    /// The permission name.
    /// </summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// An optional description of the permission.
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    /// The category the permission belongs to.
    /// </summary>
    public string? Category { get; init; }

    /// <summary>
    /// Whether this permission is system-defined and cannot be modified.
    /// </summary>
    public bool IsSystemDefined { get; init; }
}
