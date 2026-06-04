using Endatix.Api.Infrastructure;
using Endatix.Core.Abstractions.Authorization;
using Endatix.Core.UseCases.Identity.ResendUserInvitation;
using FastEndpoints;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Endatix.Api.Endpoints.Users;

/// <summary>
/// Endpoint for resending a user invitation email.
/// </summary>
public sealed class ResendUserInvitation(IMediator mediator)
    : Endpoint<UserIdRequest, Results<Ok<UserOperation>, ProblemHttpResult>>
{
    /// <summary>
    /// Configures the endpoint settings.
    /// </summary>
    public override void Configure()
    {
        Post("users/{userId}/resend-verification");
        Permissions(Actions.Tenant.InviteUsers);
        Summary(s =>
        {
            s.Summary = "Resend user invite email";
            s.Description = "Resends the activation invitation email to a pending user.";
            s.ExampleRequest = new UserIdRequest { UserId = 1 };
            s.Responses[200] = "Verification email sent successfully.";
            s.Responses[400] = "Invalid request or user already verified.";
            s.Responses[404] = "User not found.";
        });
        Description(builder => builder
            .Produces<UserOperation>(200, "application/json")
            .ProducesProblem(400)
            .ProducesProblem(404));
    }

    /// <inheritdoc/>
    public override async Task<Results<Ok<UserOperation>, ProblemHttpResult>> ExecuteAsync(
        UserIdRequest request,
        CancellationToken ct)
    {
        var result = await mediator.Send(new ResendUserInvitationCommand(request.UserId), ct);

        return TypedResultsBuilder
            .MapResult(result, result => UserOperation.Success("Verification email sent.")).SetTypedResults<Ok<UserOperation>, ProblemHttpResult>();
    }
}
