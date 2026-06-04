using Endatix.Api.Infrastructure;
using Endatix.Core.Abstractions.Authorization;
using Endatix.Core.UseCases.Identity.CancelUserInvite;
using FastEndpoints;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Endatix.Api.Endpoints.Users;

/// <summary>
/// Endpoint for cancelling a pending user invitation.
/// </summary>
public sealed class CancelUserInvite(IMediator mediator)
    : Endpoint<UserIdRequest, Results<Ok<UserOperation>, ProblemHttpResult>>
{
    /// <summary>
    /// Configures the endpoint settings.
    /// </summary>
    public override void Configure()
    {
        Delete("users/{userId}/invite");
        Permissions(Actions.Tenant.InviteUsers);
        Summary(s =>
        {
            s.Summary = "Cancel a pending user invite";
            s.Description = "Cancels a pending invitation for a tenant user by user ID.";
            s.ExampleRequest = new UserIdRequest { UserId = 1 };
            s.Responses[200] = "Invite cancelled successfully.";
            s.Responses[404] = "User or invite not found.";
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
        var result = await mediator.Send(new CancelUserInviteCommand(request.UserId), ct);

        return TypedResultsBuilder
            .MapResult(result, result => UserOperation.Success("Invite cancelled."))
            .SetTypedResults<Ok<UserOperation>, ProblemHttpResult>();
    }
}
