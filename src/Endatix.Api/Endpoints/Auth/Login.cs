using FastEndpoints;
using MediatR;
using Microsoft.AspNetCore.Http.HttpResults;
using Endatix.Core.UseCases.Identity.Login;
using Endatix.Api.Infrastructure;

namespace Endatix.Api.Endpoints.Auth;

public class Login(IMediator mediator) : Endpoint<LoginRequest, Results<Ok<LoginResponse>, ProblemHttpResult>>
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
            s.Summary = "Sign in";
            s.Description = "Authenticates a user based on valid credentials and returns JWT token and refresh token";
            s.Responses[200] = "User has been successfully authenticated";
            s.Responses[400] = "The supplied credentials are invalid!";
            s.ExampleRequest = new LoginRequest("user@example.com", "Password123!");
        });
    }

    /// <inheritdoc />
    public override async Task<Results<Ok<LoginResponse>, ProblemHttpResult>> ExecuteAsync(LoginRequest request, CancellationToken cancellationToken)
    {
        var loginCommand = new LoginCommand(request.Email, request.Password);
        var result = await mediator.Send(loginCommand, cancellationToken);

        return TypedResultsBuilder
            .MapResult(result, tokenDto => new LoginResponse(request.Email, tokenDto.AccessToken.Token, tokenDto.RefreshToken.Token))
            .SetTypedResults<Ok<LoginResponse>, ProblemHttpResult>();
    }
}
