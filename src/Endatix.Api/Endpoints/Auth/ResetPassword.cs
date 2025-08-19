using Endatix.Api.Infrastructure;
using Endatix.Core.UseCases.Account.ResetPassword;
using FastEndpoints;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Endatix.Api.Endpoints.Auth;

public class ResetPassword(
    IMediator mediator
) : Endpoint<ResetPasswordRequest, Results<Ok<string>, ProblemHttpResult>>
{
    public const string ENDPOINT_PATH = "auth/reset-password";
    /// <inheritdoc/>
    public override void Configure()
    {
        Post(ENDPOINT_PATH);
        AllowAnonymous();
        Summary(s =>
        {
            s.Summary = "Reset password";
            s.Description = "Resets a user's password.";
            s.Responses[200] = "Password reset successfully.";
        });
    }

    /// <inheritdoc/>
    public override async Task<Results<Ok<string>, ProblemHttpResult>> ExecuteAsync(ResetPasswordRequest request, CancellationToken cancellationToken)
    {
        var resetPasswordCommand = new ResetPasswordCommand(request.Email, request.ResetCode, request.NewPassword);
        var result = await mediator.Send(resetPasswordCommand, cancellationToken);

        return TypedResultsBuilder
                .FromResult(result)
                .SetTypedResults<Ok<string>, ProblemHttpResult>();
    }

    private static ProblemHttpResult InvalidTokenResult()
    {
        const string INVALID_TOKEN_MESSAGE = "Password reset link is invalid or has expired. Generate a new link and try again.";
        var invalidTokenResult = TypedResults.Problem(
            title: "Invalid token",
            detail: INVALID_TOKEN_MESSAGE,
            statusCode: StatusCodes.Status400BadRequest
        );
        invalidTokenResult.ProblemDetails.Extensions.Add("errorCode", "invalid_token");

        return invalidTokenResult;
    }
}
