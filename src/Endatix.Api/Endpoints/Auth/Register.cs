using FastEndpoints;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Endatix.Core.UseCases.Identity.Register;
using Endatix.Api.Infrastructure;

namespace Endatix.Api.Endpoints.Auth;

/// <summary>
/// Endpoint for registering new user
/// </summary>
public class Register(IMediator mediator) : Endpoint<RegisterRequest, Results<Ok<RegisterResponse>, ProblemHttpResult>>
{
    public override void Configure()
    {
        Post("auth/register");
        AllowAnonymous();
        Throttle(5, 60);
        Summary(s =>
        {
            s.Summary = "Register a new user";
            s.Description = "Creates a new user. Omit tenantSlug for an unattached account (TenantId = 0). When tenantSlug is set, the tenant must allow self-registration; the shared system DefaultRegistrationRoleName is assigned (not a cloned role).";
            s.Responses[200] = "Registration accepted. Returned whether or not the address was already registered.";
            s.Responses[400] = "Registration failed. Please check your input and try again.";
            s.Responses[403] = "Self-registration is not enabled for this tenant.";
            s.Responses[404] = "Unknown tenant public id.";
            s.Responses[429] = "Too many requests.";
            s.ExampleRequest = new RegisterRequest("user@example.com", "Password123!", "Password123!");
            s.ResponseExamples[200] = new RegisterResponse(Success: true, Message: RegisterHandler.GENERAL_SUCCESS_MESSAGE);
        });
        Description(builder => builder
            .Produces<RegisterResponse>(StatusCodes.Status200OK, "application/json")
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status429TooManyRequests));
    }

    /// <inheritdoc/>
    public override async Task<Results<Ok<RegisterResponse>, ProblemHttpResult>> ExecuteAsync(RegisterRequest request, CancellationToken ct)
    {
        var registerUserCommand = new RegisterCommand(request.Email, request.Password, request.TenantSlug);
        var userRegistrationResult = await mediator.Send(registerUserCommand, ct);

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
                .MapResult(userRegistrationResult, (message) => new RegisterResponse(Success: true, Message: message))
                .SetErrorMessage(errorMessage)
                .SetTypedResults<Ok<RegisterResponse>, ProblemHttpResult>();
    }
}
