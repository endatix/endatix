using Endatix.Api.Infrastructure;
using Endatix.Core.Abstractions.Authorization;
using Endatix.Core.UseCases.Identity.LockoutUser;
using FastEndpoints;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Endatix.Api.Endpoints.Users;

/// <summary>
/// Endpoint for locally locking out a tenant user.
/// </summary>
public sealed class LockoutUser(IMediator mediator)
    : Endpoint<UserIdRequest, Results<Ok<UserOperation>, ProblemHttpResult>>
{
    /// <inheritdoc />
    public override void Configure()
    {
        Post("users/{userId}/lockout");
        Permissions(Actions.Tenant.ManageUsers);
        Summary(s =>
        {
            s.Summary = "Lock out user";
            s.Description = "Locally blocks the user from authorizing in the current tenant.";
            s.ExampleRequest = new UserIdRequest { UserId = 1 };
            s.Responses[200] = "User locked out successfully.";
            s.Responses[404] = "User not found.";
        });
        Description(builder => builder
            .Produces<UserOperation>(200, "application/json")
            .ProducesProblem(404));
    }

    /// <inheritdoc />
    public override async Task<Results<Ok<UserOperation>, ProblemHttpResult>> ExecuteAsync(
        UserIdRequest request,
        CancellationToken ct)
    {
        var result = await mediator.Send(new LockoutUserCommand(request.UserId), ct);

        return TypedResultsBuilder
            .MapResult(result, result => UserOperation.Success("User locked out."))
            .SetTypedResults<Ok<UserOperation>, ProblemHttpResult>();
    }
}
