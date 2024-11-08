﻿using FastEndpoints;
using MediatR;
using Endatix.Core.Infrastructure.Result;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Http;
using Endatix.Core.UseCases.Identity.Login;

namespace Endatix.Api.Endpoints.Auth;

public class Login(IMediator mediator) : Endpoint<LoginRequest, Results<Ok<LoginResponse>, BadRequest<IEnumerable<ValidationError>>>>
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
            var successfulResponse = new LoginResponse(request.Email, result.Value.AccessToken.Token, result.Value.RefreshToken.Token);
            return TypedResults.Ok(successfulResponse);
        }
    }
}
