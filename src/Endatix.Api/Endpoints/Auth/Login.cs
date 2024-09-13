using FastEndpoints;
using MediatR;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.UseCases.Security;
using Endatix.Core.UseCases.Security.Login;

namespace Endatix.Api.Endpoints.Auth;

public class Login(IMediator _mediator) : Endpoint<LoginRequest, LoginResponse>
{
    public override void Configure()
    {
        Post("/auth/login");
        AllowAnonymous();
        Summary(s =>
        {
            s.Summary = "Log in";
            s.Description = "Authenticates an user based of valid credentials and returns JWT token";
            s.Responses[200] = "Use has been successfully authenticated";
            s.Responses[400] = "The supplied credentials are invalid!";
        });
    }

    public override async Task HandleAsync(LoginRequest request, CancellationToken cancellationToken)
    {
        var loginCommand = new LoginCommand(request.Email, request.Password);
        Result<TokenDto> result = await _mediator.Send(loginCommand, cancellationToken);

        if (result.IsInvalid())
        {
            ThrowError("The supplied credentials are invalid!");
        }
        else
        {
            LoginResponse successfulResponse = new()
            {
                Email = request.Email,
                Token = result.Value.Token
            };
            await SendOkAsync(successfulResponse, cancellationToken);
        }
    }
}
