using FastEndpoints;
using MediatR;
using Errors = Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http.HttpResults;
using Endatix.Api.Infrastructure;
using Endatix.Core.UseCases.MyAccount.ChangePassword;
using Endatix.Core.Abstractions.Authorization;
using Endatix.Core.Abstractions;

namespace Endatix.Api.Endpoints.MyAccount;

/// <summary>
/// Endpoint for changing a user's password
/// </summary>
public class ChangePassword(IMediator mediator, IUserContext userContext) : Endpoint<ChangePasswordRequest, Results<Ok<ChangePasswordResponse>, ProblemHttpResult>>
{
    /// <summary>
    /// Configures the endpoint settings for the password change functionality.
    /// </summary>
    public override void Configure()
    {
        Post("my-account/change-password");
        Summary(s =>
        {
            s.Summary = "Changes a user's password";
            s.Description = "Allows an authenticated user to change their password.";
            s.Responses[200] = "Password changed successfully.";
            s.Responses[400] = "Invalid request or current password.";
        });
    }

    /// <summary>
    /// Executes the password change functionality
    /// </summary>
    /// <param name="request">The password change request containing the current and new password</param>
    /// <param name="cancellationToken">Cancellation token for the async operation</param>
    public override async Task<Results<Ok<ChangePasswordResponse>, ProblemHttpResult>> ExecuteAsync(ChangePasswordRequest request, CancellationToken cancellationToken)
    {
        var userIdString = userContext.GetCurrentUserId();

        var changePasswordCmd = new ChangePasswordCommand(
            long.TryParse(userIdString, out var userId) ? userId : null,
            request.CurrentPassword,
            request.NewPassword
        );

        var result = await mediator.Send(changePasswordCmd, cancellationToken);

        return TypedResultsBuilder
            .MapResult(result, message => new ChangePasswordResponse(message))
            .SetTypedResults<Ok<ChangePasswordResponse>, ProblemHttpResult>();
    }
}