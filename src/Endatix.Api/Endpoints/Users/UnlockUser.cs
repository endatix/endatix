using Endatix.Api.Infrastructure;
using Endatix.Core.Abstractions.Authorization;
using Endatix.Core.UseCases.Identity.UnlockUser;
using FastEndpoints;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Endatix.Api.Endpoints.Users;

/// <summary>
/// Endpoint for clearing a user's local lockout.
/// </summary>
public sealed class UnlockUser(IMediator mediator)
    : Endpoint<UserIdRequest, Results<Ok<UserOperation>, ProblemHttpResult>>
{
    /// <inheritdoc />
    public override void Configure()
    {
        Post("users/{userId}/unlock");
        Permissions(Actions.Tenant.ManageUsers);
        Summary(s =>
        {
            s.Summary = "Unlock user";
            s.Description = "Clears the user's local lockout in the current tenant.";
            s.ExampleRequest = new UserIdRequest { UserId = 1 };
            s.Responses[200] = "User unlocked successfully.";
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
        var result = await mediator.Send(new UnlockUserCommand(request.UserId), ct);

        return TypedResultsBuilder
            .MapResult(result, result => UserOperation.Success("User unlocked."))
            .SetTypedResults<Ok<UserOperation>, ProblemHttpResult>();
    }
}
