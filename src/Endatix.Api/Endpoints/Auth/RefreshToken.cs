using FastEndpoints;
using MediatR;
using Errors = Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http.HttpResults;
using Endatix.Api.Infrastructure;
using Endatix.Core.UseCases.Identity.RefreshToken;

namespace Endatix.Api.Endpoints.Auth;

/// <summary>
/// Endpoint for refreshing the access token using a refresh token
/// </summary>
public class RefreshToken(IMediator mediator) : Endpoint<RefreshTokenRequest, Results<Ok<RefreshTokenResponse>, BadRequest<Errors.ProblemDetails>>>
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

    /// <inheritdoc/>
    public override async Task<Results<Ok<RefreshTokenResponse>, BadRequest<Errors.ProblemDetails>>> ExecuteAsync(RefreshTokenRequest request, CancellationToken cancellationToken)
    {
        var authHeader = request.Authorization;
        var accessToken = authHeader!["Bearer ".Length..].Trim();
        var refreshCommand = new RefreshTokenCommand(accessToken, request.RefreshToken!);
        var result = await mediator.Send(refreshCommand, cancellationToken);

        return TypedResultsBuilder
                .MapResult(result, (tokenDto) => new RefreshTokenResponse(tokenDto.AccessToken.Token, tokenDto.RefreshToken.Token))
                .SetErrorMessage("Invalid or expired token.")
                .SetTypedResults<Ok<RefreshTokenResponse>, BadRequest<Errors.ProblemDetails>>();
    }
}
