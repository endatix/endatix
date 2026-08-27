using Endatix.Api.Infrastructure;
using Endatix.Core.UseCases.Account.ResetPassword;
using FastEndpoints;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Endatix.Api.Endpoints.Account;

public class ResetPassword(
    IMediator mediator
) : Endpoint<ResetPasswordRequest, Results<Ok<string>, ProblemHttpResult>>
{
    public override void Configure()
    {
        Post("account/reset-password");
        AllowAnonymous();
        Summary(s =>
        {
            s.Summary = "Reset password";
            s.Description = "Resets a user's password.";
            s.Responses[200] = "Password reset successfully.";
        });
        Description(builder => builder
            .Produces<string>(StatusCodes.Status200OK, "application/json")
            .ProducesProblem(StatusCodes.Status400BadRequest));
    }

    /// <inheritdoc/>
    public override async Task<Results<Ok<string>, ProblemHttpResult>> ExecuteAsync(ResetPasswordRequest request, CancellationToken ct)
    {
        var resetPasswordCommand = new ResetPasswordCommand(request.Email, request.ResetCode, request.NewPassword);
        var result = await mediator.Send(resetPasswordCommand, ct);

        return TypedResultsBuilder
                .FromResult(result)
                .SetTypedResults<Ok<string>, ProblemHttpResult>();
    }
}
