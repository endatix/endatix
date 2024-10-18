using FastEndpoints;
using MediatR;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.UseCases.Identity.Login;

namespace Endatix.Api.Endpoints.Auth;

/// <summary>
/// Endpoint for user authentication
/// </summary>
public class Login(IMediator mediator) : Endpoint<LoginRequest, LoginResponse>
{
    /// <summary>
    /// Configures the endpoint
    /// </summary>
    public override void Configure()
    {
        Post("auth/login");
        AllowAnonymous();
        Summary(s =>
        {
            s.Summary = "Log in";
            s.Description = "Authenticates a user based on valid credentials and returns JWT token and refresh token";
            s.Responses[200] = "User has been successfully authenticated";
            s.Responses[400] = "The supplied credentials are invalid!";
            s.ExampleRequest = new LoginRequest("user@example.com", "Password123!");
        });
    }

    /// <summary>
    /// Handles the login request
    /// </summary>
    /// <param name="request">The login request containing user credentials</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>A task representing the asynchronous operation</returns>
    public override async Task HandleAsync(LoginRequest request, CancellationToken cancellationToken)
    {
        var loginCommand = new LoginCommand(request.Email, request.Password);
        var result = await mediator.Send(loginCommand, cancellationToken);

        if (result.IsInvalid())
        {
            ThrowError("The supplied credentials are invalid!");
        }
        else
        {
            var successfulResponse = new LoginResponse(request.Email, result.Value.AccessToken.Token, result.Value.RefreshToken.Token);
            await SendOkAsync(successfulResponse, cancellationToken);
        }
    }
}
