using FastEndpoints;
using MediatR;
using Microsoft.AspNetCore.Http.HttpResults;
using Endatix.Api.Infrastructure;
using Endatix.Core.UseCases.Identity.Login;
using Endatix.Core.Abstractions.Authorization;

namespace Endatix.Api.Endpoints.Auth;

public class Logout(IMediator mediator) : EndpointWithoutRequest<Results<Ok<LogoutResponse>, BadRequest>>
{
    /// <summary>
    /// Configures the endpoint settings for the logout functionality.
    /// </summary>
    public override void Configure()
    {
        Post("auth/logout");
        Summary(s =>
        {
            s.Summary = "Logs out the authenticated user";
            s.Description = "Initiates the logout process for the authenticated user.";
            s.Responses[200] = "User logged out successfully.";
            s.Responses[400] = "Invalid request or authentication state.";
        });
    }

    /// <summary>
    /// Executes the logout functionality
    /// </summary>
    /// <param name="cancellationToken">Cancellation token for the async operation.</param>
    public override async Task<Results<Ok<LogoutResponse>, BadRequest>> ExecuteAsync(CancellationToken cancellationToken)
    {

        var logoutUserCmd = new LogoutCommand(User);
        var logoutResult = await mediator.Send(logoutUserCmd, cancellationToken);

        return TypedResultsBuilder
                .MapResult(logoutResult, (message) => new LogoutResponse(message))
                .SetTypedResults<Ok<LogoutResponse>, BadRequest>();
    }
}