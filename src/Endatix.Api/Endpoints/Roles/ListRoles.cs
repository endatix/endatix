using Endatix.Api.Common;
using Endatix.Api.Infrastructure;
using Endatix.Core.Abstractions.Authorization;
using Endatix.Core.Entities.Identity;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.UseCases.Identity.ListRoles;
using FastEndpoints;
using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Endatix.Api.Endpoints.Roles;

/// <summary>
/// Endpoint for listing roles in the current tenant.
/// </summary>
public sealed class ListRoles(IMediator mediator)
    : Endpoint<ListRolesRequest, Results<Ok<Paged<ListRolesResponse>>, ProblemHttpResult>>
{
    /// <summary>
    /// Configures the endpoint settings.
    /// </summary>
    public override void Configure()
    {
        Get("roles");
        Permissions(Actions.Tenant.ViewRoles);
        Summary(s =>
        {
            s.Summary = "List roles";
            s.Description = "Retrieves roles with paging and optional role type filtering (all, system, custom).";
            s.Responses[200] = "Roles retrieved successfully.";
            s.Responses[400] = "Invalid request.";
        });
        Description(builder => builder
            .Produces<Paged<ListRolesResponse>>(200, "application/json")
            .ProducesProblem(400));
    }

    /// <inheritdoc/>
    public override async Task<Results<Ok<Paged<ListRolesResponse>>, ProblemHttpResult>> ExecuteAsync(
        ListRolesRequest request,
        CancellationToken ct)
    {
        var query = new ListRolesQuery(request.Page, request.PageSize, request.RoleType, request.Search);
        var result = await mediator.Send(query, ct);

        return TypedResultsBuilder
            .MapResult(result, Map)
            .SetTypedResults<Ok<Paged<ListRolesResponse>>, ProblemHttpResult>();
    }

    /// <summary>
    /// Maps the paged roles to the list roles response.
    /// </summary>
    private static Paged<ListRolesResponse> Map(Paged<RoleListItem> pagedRoles)
    {
        var items = pagedRoles.Items
            .Select(role => new ListRolesResponse
            {
                Id = role.Id,
                Name = role.Name,
                Description = role.Description,
                IsSystemDefined = role.IsSystemDefined,
                IsActive = role.IsActive,
                Permissions = role.Permissions,
                UsersCount = role.UsersCount
            })
            .ToList();

        return new Paged<ListRolesResponse>(
            pagedRoles.Page,
            pagedRoles.PageSize,
            pagedRoles.TotalRecords,
            pagedRoles.TotalPages,
            items);
    }
}

/// <summary>
/// Request for listing roles.
/// </summary>
public sealed record ListRolesRequest : IPageable
{
    public int? Page { get; set; }

    public int? PageSize { get; set; }

    public string? RoleType { get; set; }

    public string? Search { get; set; }
}

/// <summary>
/// Validator for the ListRolesRequest.
/// </summary>
public sealed class ListRolesValidator : Validator<ListRolesRequest>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ListRolesValidator"/> class.
    /// </summary>
    public ListRolesValidator()
    {
        Include(new PageableRequestValidator());

        RuleFor(x => x.PageSize)
            .LessThanOrEqualTo(ListRolesQuery.MaxPageSize)
            .When(x => x.PageSize.HasValue);

        RuleFor(x => x.RoleType)
            .Must(roleType =>
            {
                var normalized = roleType?.Trim();
                return string.IsNullOrEmpty(normalized) ||
                    string.Equals(normalized, "all", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(normalized, "system", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(normalized, "custom", StringComparison.OrdinalIgnoreCase);
            })
            .WithMessage("RoleType must be 'all', 'system', or 'custom'.");

        RuleFor(x => x.Search)
            .MaximumLength(256)
            .When(x => x.Search is not null);
    }
}

public sealed record ListRolesResponse
{
    public long Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string? Description { get; init; }
    public bool IsSystemDefined { get; init; }
    public bool IsActive { get; init; }
    public IReadOnlyList<string> Permissions { get; init; } = [];
    public int UsersCount { get; init; }
}