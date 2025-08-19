using System;
using System.Text;
using System.Text.Encodings.Web;
using Endatix.Api.Infrastructure;
using Endatix.Core.Abstractions;
using Endatix.Core.Features.Email;
using Endatix.Infrastructure.Identity;
using FastEndpoints;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Logging;

namespace Endatix.Api.Endpoints.Auth;

public class ForgotPassword(
        UserManager<AppUser> userManager,
        IEmailSender emailSender,
        ILogger<ForgotPassword> logger
    ) :
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
        const string GENERAL_MESSAGE = "Thank you. If an account exists with this email, you will receive an email with instructions to reset your password.";

        var user = await userManager.FindByEmailAsync(request.Email);
        if (user is not null && await userManager.IsEmailConfirmedAsync(user))
        {
            var token = await userManager.GeneratePasswordResetTokenAsync(user);
            var resetCode = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(token));
            var query = $"email={HtmlEncoder.Default.Encode(request.Email)}&resetCode={HtmlEncoder.Default.Encode(resetCode)}";
            logger.LogInformation("Reset password query: {Query}", query);
            var endatixHubBaseUrl = "http://localhost:3000";
            var callbackUrl = new Uri($"{endatixHubBaseUrl}/reset-password?{query}");
            var resetPasswordEmail = new EmailWithBody()
            {
                To = request.Email,
                From = "no-reply@endatix.com",
                Subject = "Reset Password",
                HtmlBody = "Please reset your password by clicking here: <a href=\"" + callbackUrl + "\">link</a>",
                PlainTextBody = "Please reset your password by clicking here: " + callbackUrl
            };
            await emailSender.SendEmailAsync(resetPasswordEmail, cancellationToken);
        }

        return TypedResults.Ok(new ForgotPasswordResponse { Message = GENERAL_MESSAGE });
    }
}