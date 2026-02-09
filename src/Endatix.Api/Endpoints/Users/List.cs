using FastEndpoints;
using MediatR;
using Microsoft.AspNetCore.Http.HttpResults;
using Endatix.Core.Abstractions.Authorization;
using Endatix.Core.Entities.Identity;
using Endatix.Core.UseCases.Identity.ListUsers;
using Endatix.Api.Infrastructure;

namespace Endatix.Api.Endpoints.Users;

/// <summary>
/// Endpoint for listing users in the current tenant (admin-only).
/// </summary>
public class List(IMediator mediator)
    : Endpoint<ListUsersRequest, Results<Ok<IEnumerable<ListUsersResponse>>, ProblemHttpResult>>
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
            s.Responses[200] = "Users retrieved successfully.";
        });
    }

    /// <inheritdoc/>
    public override async Task<Results<Ok<IEnumerable<ListUsersResponse>>, ProblemHttpResult>> ExecuteAsync(
        ListUsersRequest request,
        CancellationToken ct)
    {
        var listUsersQuery = new ListUsersQuery(request.Page, request.PageSize);
        var result = await mediator.Send(listUsersQuery, ct);

        return TypedResultsBuilder
            .MapResult(result, Map)
            .SetTypedResults<Ok<IEnumerable<ListUsersResponse>>, ProblemHttpResult>();
    }

    private static IEnumerable<ListUsersResponse> Map(IEnumerable<UserWithRoles> items) =>
        items.Select(u => new ListUsersResponse
        {
            Id = u.Id,
            UserName = u.UserName,
            Email = u.Email,
            IsVerified = u.IsVerified,
            Roles = u.Roles
        });
}
