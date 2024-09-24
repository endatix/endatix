
using Endatix.Core.UseCases.Register;
using FastEndpoints;
using MediatR;

namespace Endatix.Api.Endpoints.Auth;

/// <summary>
/// Endpoint for registering new user
/// </summary>
public class Register(IMediator mediator) : Endpoint<RegisterRequest, RegisterResponse>
{
    public override void Configure()
    {
        Post("/auth/register");
        AllowAnonymous();
        Summary(s =>
        {
            s.Summary = "Register a new user";
            s.Description = "Creates a new user account in the Endatix application using the provided email and password.";
            s.Responses[200] = "User has been successfully registered.";
            s.Responses[400] = "Registration failed. Please check your input and try again.";
            s.ExampleRequest = new RegisterRequest("user@example.com", "Password123!", "Password123!");
        });
    }

    public override async Task HandleAsync(RegisterRequest request, CancellationToken cancellationToken)
    {
        var registerUserCommand = new RegisterCommand(request.Email, request.Password);
        var result = await mediator.Send(registerUserCommand, cancellationToken);

        if (!result.IsSuccess)
        {
            ThrowError("Registration failed. Please check your input and try again.");
        }

        var successfulResponse = new RegisterResponse(Success: true, Message: "User has been successfully registered");

        await SendOkAsync(successfulResponse, cancellationToken);
    }
}