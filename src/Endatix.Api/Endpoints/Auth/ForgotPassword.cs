using Endatix.Api.Infrastructure;
using Endatix.Core.UseCases.Account.ForgotPassword;
using FastEndpoints;
using MediatR;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Endatix.Api.Endpoints.Auth;

public class ForgotPassword(IMediator mediator) :
    Endpoint<ForgotPasswordRequest, Results<Ok<ForgotPasswordResponse>, ProblemHttpResult>>
{
    public override void Configure()
    {
        Post("auth/forgot-password");
        AllowAnonymous();
        Summary(s =>
        {
            s.Summary = "Forgot password";
            s.Description = "Sends a password reset email to the user.";
            s.Responses[200] = "Password reset email sent successfully.";
            s.Responses[400] = "Invalid request or email.";
            s.ExampleRequest = new { Email = "user@example.com" };
        });
    }

    public override async Task<Results<Ok<ForgotPasswordResponse>, ProblemHttpResult>> ExecuteAsync(ForgotPasswordRequest request, CancellationToken cancellationToken)
    {
        var forgotPasswordCommand = new ForgotPasswordCommand(request.Email);
        var result = await mediator.Send(forgotPasswordCommand, cancellationToken);

        return TypedResultsBuilder
                 .MapResult(result, (message) => new ForgotPasswordResponse { Message = message })
                 .SetTypedResults<Ok<ForgotPasswordResponse>, ProblemHttpResult>();
    }
}