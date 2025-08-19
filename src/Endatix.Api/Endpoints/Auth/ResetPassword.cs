using System.Text;
using Endatix.Core.Infrastructure.Attributes;
using Endatix.Infrastructure.Identity;
using FastEndpoints;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Logging;

namespace Endatix.Api.Endpoints.Auth;

public class ResetPassword(
    UserManager<AppUser> userManager,
    ILogger<ResetPassword> logger
) : Endpoint<ResetPasswordRequest, Results<Ok, ProblemHttpResult>>
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
    public override async Task<Results<Ok, ProblemHttpResult>> ExecuteAsync(ResetPasswordRequest request, CancellationToken cancellationToken)
    {
        var user = await userManager.FindByEmailAsync(request.Email);
        if (user is null || !await userManager.IsEmailConfirmedAsync(user))
        {
            return InvalidTokenResult();
        }

        var code = Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(request.ResetCode));
        var isValid = await userManager.VerifyUserTokenAsync(user, userManager.Options.Tokens.PasswordResetTokenProvider, UserManager<AppUser>.ResetPasswordTokenPurpose, code);

        IdentityResult result;
        try
        {
            result = await userManager.ResetPasswordAsync(user, code, request.NewPassword);
        }
        catch (FormatException)
        {
            return InvalidTokenResult();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "An unexpected error occurred while resetting the password");
            return TypedResults.Problem(
                title: "An unexpected error occurred",
                detail: "The server encountered an unexpected error. Please request a new password reset link and try again.",
                statusCode: StatusCodes.Status500InternalServerError
            );
        }

        if (!result.Succeeded)
        {
            return InvalidTokenResult();
        }

        return TypedResults.Ok();
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
