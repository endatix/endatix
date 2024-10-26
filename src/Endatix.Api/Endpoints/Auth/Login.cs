using FastEndpoints;
using MediatR;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.UseCases.Security.Login;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Http;

namespace Endatix.Api.Endpoints.Auth;

public class Login(IMediator mediator) : Endpoint<LoginRequest, Results<Ok<LoginResponse>, BadRequest<IEnumerable<ValidationError>>>>
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

    /// <inheritdoc />
    public override async Task<Results<Ok<LoginResponse>, BadRequest<IEnumerable<ValidationError>>>> ExecuteAsync(LoginRequest request, CancellationToken cancellationToken)
    {
        var loginCommand = new LoginCommand(request.Email, request.Password);
        var result = await mediator.Send(loginCommand, cancellationToken);

        if (result.IsInvalid())
        {
            return TypedResults.BadRequest(result.ValidationErrors);
        }
        else
        {
            LoginResponse successfulResponse = new()
            {
                Email = request.Email,
                Token = result.Value.Token
            };
            return TypedResults.Ok(successfulResponse);
        }
    }
}
