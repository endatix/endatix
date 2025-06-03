using FastEndpoints;
using MediatR;
using Microsoft.AspNetCore.Http.HttpResults;
using Errors = Microsoft.AspNetCore.Mvc;
using Endatix.Core.UseCases.Identity.Register;
using Endatix.Infrastructure.Identity.Authorization;
using Endatix.Api.Infrastructure;

namespace Endatix.Api.Endpoints.Auth;

/// <summary>
/// Endpoint for registering new user
/// </summary>
public class Register(IMediator mediator) : Endpoint<RegisterRequest, Results<Ok<RegisterResponse>, BadRequest<Errors.ProblemDetails>>>
{
    public override void Configure()
    {
        Post("auth/register");
        Permissions(Allow.AllowAll);
        Summary(s =>
        {
            s.Summary = "Register a new user";
            s.Description = "Creates a new user account in the Endatix application using the provided email and password.";
            s.Responses[200] = "User has been successfully registered.";
            s.Responses[400] = "Registration failed. Please check your input and try again.";
            s.ExampleRequest = new RegisterRequest("user@example.com", "Password123!", "Password123!");
        });
    }

    /// <inheritdoc/>
    public override async Task<Results<Ok<RegisterResponse>, BadRequest<Errors.ProblemDetails>>> ExecuteAsync(RegisterRequest request, CancellationToken cancellationToken)
    {
        var registerUserCommand = new RegisterCommand(request.Email, request.Password);
        var userRegistrationResult = await mediator.Send(registerUserCommand, cancellationToken);

        var errorMessage = "Registration failed. ";
        if (userRegistrationResult.Status == Core.Infrastructure.Result.ResultStatus.Invalid && userRegistrationResult.ValidationErrors.Any())
        {
            errorMessage += userRegistrationResult.ValidationErrors.First().ErrorMessage;
        }
        else
        {
            errorMessage += "Please check your input and try again.";
        }

        return TypedResultsBuilder
                .MapResult(userRegistrationResult, (user) => new RegisterResponse(Success: true, Message: "User has been successfully registered"))
                .SetErrorMessage(errorMessage)
                .SetTypedResults<Ok<RegisterResponse>, BadRequest<Errors.ProblemDetails>>();
    }
}