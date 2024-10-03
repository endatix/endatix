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
            s.ExampleRequest = new RefreshTokenRequest(1234567890, "your-refresh-token-here");
        });
    }

    public override async Task HandleAsync(RefreshTokenRequest request, CancellationToken cancellationToken)
    {
        var refreshCommand = new RefreshTokenCommand(request.UserId, request.RefreshToken);
        var result = await mediator.Send(refreshCommand, cancellationToken);

        if (result.IsInvalid())
        {
            ThrowError("Invalid or expired refresh token.");
        }
        else
        {
            var response = new RefreshTokenResponse(result.Value.AccessToken.Token, result.Value.RefreshToken.Token);
            await SendOkAsync(response, cancellationToken);
        }
    }
}
