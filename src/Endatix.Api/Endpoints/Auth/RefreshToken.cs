using FastEndpoints;
using MediatR;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.UseCases.Identity.RefreshToken;

namespace Endatix.Api.Endpoints.Auth;

/// <summary>
/// Endpoint for refreshing the access token using a refresh token
/// </summary>
public class RefreshToken(IMediator mediator) : Endpoint<RefreshTokenRequest, RefreshTokenResponse>
{
    /// <summary>
    /// Configures the endpoint
    /// </summary>
    public override void Configure()
    {
        Post("auth/refresh-token");
        AllowAnonymous();
        Summary(s =>
        {
            s.Summary = "Refresh the access token";
            s.Description = "Generates a new access token using a valid refresh token.";
            s.Responses[200] = "Access token successfully refreshed.";
            s.Responses[400] = "Invalid or expired refresh token.";
            s.ExampleRequest = new { RefreshToken = "example-refresh-token" };
        });
    }

    /// <summary>
    /// Handles the refresh token request.
    /// </summary>
    /// <param name="request">The request containing the authorization header and refresh token.</param>
    /// <param name="cancellationToken">Cancellation token for the request.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public override async Task HandleAsync(RefreshTokenRequest request, CancellationToken cancellationToken)
    {
        var authHeader = request.Authorization;
        var accessToken = authHeader!["Bearer ".Length..].Trim();
        var refreshCommand = new RefreshTokenCommand(accessToken, request.RefreshToken!);
        var result = await mediator.Send(refreshCommand, cancellationToken);

        if (result.IsInvalid())
        {
            ThrowError("Invalid or expired token.");
        }
        else
        {
            var response = new RefreshTokenResponse(result.Value.AccessToken.Token, result.Value.RefreshToken.Token);
            await SendOkAsync(response, cancellationToken);
        }
    }
}
