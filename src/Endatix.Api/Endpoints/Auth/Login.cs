using FastEndpoints;
using MediatR;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.UseCases.Identity.Login;

namespace Endatix.Api.Endpoints.Auth;

public class Login(IMediator mediator) : Endpoint<LoginRequest, LoginResponse>
{
    public override void Configure()
    {
        Post("/auth/login");
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
            LoginResponse successfulResponse = new()
            {
                Email = request.Email,
                Token = result.Value.Token,
                RefreshToken = string.Empty
            };
            await SendOkAsync(successfulResponse, cancellationToken);
        }
    }
}
