using Endatix.Api.Infrastructure;
using Endatix.Core.Abstractions.Authorization;
using Endatix.Core.UseCases.Identity.RemoveUserAccess;
using FastEndpoints;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Endatix.Api.Endpoints.Users;

/// <summary>
/// Endpoint for removing a user's access to the current tenant without deleting their global identity.
/// </summary>
public sealed class RemoveUserAccess(IMediator mediator)
    : Endpoint<UserIdRequest, Results<Ok<UserOperation>, ProblemHttpResult>>
{
    /// <summary>
    /// Configures the endpoint settings.
    /// </summary>
    public override void Configure()
    {
        Delete("users/{userId}");
        Permissions(Actions.Tenant.ManageUsers);
        Summary(s =>
        {
            s.Summary = "Remove user access";
            s.Description = "Removes the user's access to the current tenant without deleting their global identity.";
            s.ExampleRequest = new UserIdRequest { UserId = 1 };
            s.Responses[200] = "User access removed successfully.";
            s.Responses[404] = "User not found.";
        });
        Description(builder => builder
            .Produces<UserOperation>(200, "application/json")
            .ProducesProblem(404));
    }

    /// <inheritdoc/>
    public override async Task<Results<Ok<UserOperation>, ProblemHttpResult>> ExecuteAsync(
        UserIdRequest request,
        CancellationToken ct)
    {
        var result = await mediator.Send(
            new RemoveUserAccessCommand(request.UserId),
            ct);

        return TypedResultsBuilder
            .MapResult(result, result => UserOperation.Success("User access removed."))
            .SetTypedResults<Ok<UserOperation>, ProblemHttpResult>();
    }
}
