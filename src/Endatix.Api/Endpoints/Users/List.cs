using Endatix.Api.Common;
using Endatix.Api.Infrastructure;
using Endatix.Core.Abstractions.Authorization;
using Endatix.Core.Entities.Identity;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.UseCases.Identity.ListUsers;
using FastEndpoints;
using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Endatix.Api.Endpoints.Users;

/// <summary>
/// Endpoint for listing users in the current tenant (admin-only).
/// </summary>
public sealed class List(IMediator mediator)
    : Endpoint<ListUsersRequest, Results<Ok<Paged<ListUsersResponse>>, ProblemHttpResult>>
{
    /// <summary>
    /// Configures the endpoint settings.
    /// </summary>
    public override void Configure()
    {
        Get("users");
        Permissions(Actions.Tenant.ViewUsers);
        Summary(s =>
        {
            s.Summary = "List users";
            s.Description = "Lists all users for the current tenant. Multi-tenancy is assumed.";
            s.ExampleRequest = new ListUsersRequest
            {
                Page = 1,
                PageSize = 10,
                Search = null,
                Role = null,
                Status = null
            };
            s.Responses[200] = "Users retrieved successfully.";
            s.Responses[400] = "Invalid input data.";
        });
        Description(builder => builder
            .Produces<Paged<ListUsersResponse>>(200, "application/json")
            .ProducesProblem(400));
    }

    /// <inheritdoc/>
    public override async Task<Results<Ok<Paged<ListUsersResponse>>, ProblemHttpResult>> ExecuteAsync(
        ListUsersRequest request,
        CancellationToken ct)
    {
        var listUsersQuery = new ListUsersQuery(
            request.Page,
            request.PageSize,
            request.Search,
            request.Role,
            request.Status);
        var result = await mediator.Send(listUsersQuery, ct);

        return TypedResultsBuilder
            .MapResult(result, Map)
            .SetTypedResults<Ok<Paged<ListUsersResponse>>, ProblemHttpResult>();
    }

    private static Paged<ListUsersResponse> Map(Paged<UserWithRoles> pagedUsers)
    {
        var items = pagedUsers.Items
            .Select(u => new ListUsersResponse
        {
            Id = u.Id,
            UserName = u.UserName,
            Email = u.Email,
            IsVerified = u.IsVerified,
            Roles = u.Roles,
            AuthProvider = u.AuthProvider,
            IsExternal = u.IsExternal,
            IsLockedOut = u.IsLockedOut,
            DisplayName = u.DisplayName,
            LastLoginAt = u.LastLoginAt
        })
            .ToList();

        return new Paged<ListUsersResponse>(
            pagedUsers.Page,
            pagedUsers.PageSize,
            pagedUsers.TotalRecords,
            pagedUsers.TotalPages,
            items);
    }
}

/// <summary>
/// Request for listing users in the current tenant. Tenant filter is implicit.
/// </summary>
public record ListUsersRequest : IPagedRequest
{
    /// <inheritdoc />
    public int? Page { get; set; }

    /// <inheritdoc />
    public int? PageSize { get; set; }

    /// <summary>
    /// Searches by user name or email.
    /// </summary>
    public string? Search { get; set; }

    /// <summary>
    /// Filters by assigned role name.
    /// </summary>
    public string? Role { get; set; }

    /// <summary>
    /// Filters by invitation status: active or pending.
    /// </summary>
    public string? Status { get; set; }
}

/// <summary>
/// Response model for a user in the list users endpoint.
/// </summary>
public record ListUsersResponse
{
    /// <summary>
    /// The user's unique identifier.
    /// </summary>
    public long Id { get; init; }

    /// <summary>
    /// The user's display name.
    /// </summary>
    public string UserName { get; init; } = null!;

    /// <summary>
    /// The user's email address.
    /// </summary>
    public string? Email { get; init; }

    /// <summary>
    /// Indicates whether the user's email is verified.
    /// </summary>
    public bool IsVerified { get; init; }

    /// <summary>
    /// The role names assigned to the user.
    /// </summary>
    public IReadOnlyList<string> Roles { get; init; } = [];

    /// <summary>
    /// Authentication provider for this user.
    /// </summary>
    public string AuthProvider { get; init; } = string.Empty;

    /// <summary>
    /// Indicates whether the user is managed by an external provider.
    /// </summary>
    public bool IsExternal { get; init; }

    /// <summary>
    /// Indicates whether the user is locally locked out.
    /// </summary>
    public bool IsLockedOut { get; init; }

    /// <summary>
    /// External or friendly display name.
    /// </summary>
    public string? DisplayName { get; init; }

    /// <summary>
    /// Last successful login timestamp.
    /// </summary>
    public DateTimeOffset? LastLoginAt { get; init; }
}

/// <summary>
/// Validator for the <see cref="ListUsersRequest"/>.
/// </summary>
public class ListUsersValidator : Validator<ListUsersRequest>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ListUsersValidator"/> class.
    /// </summary>
    public ListUsersValidator()
    {
        Include(new PagedRequestValidator());

        RuleFor(x => x.PageSize)
            .LessThanOrEqualTo(ListUsersQuery.MaxPageSize)
            .When(x => x.PageSize.HasValue);

        RuleFor(x => x.Search)
            .MaximumLength(256)
            .When(x => x.Search is not null);

        RuleFor(x => x.Role)
            .MaximumLength(256)
            .When(x => x.Role is not null);

        RuleFor(x => x.Status)
            .Must(status => status is null || IsKnownStatus(status))
            .WithMessage("Status must be either 'active' or 'pending'.");
    }

    private static bool IsKnownStatus(string status)
    {
        return string.Equals(status, "active", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(status, "pending", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(status, "locked", StringComparison.OrdinalIgnoreCase);
    }
}
