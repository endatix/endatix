using FastEndpoints;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Endatix.Core.UseCases.Identity.VerifyEmail;
using Endatix.Api.Infrastructure;

namespace Endatix.Api.Endpoints.Auth;

/// <summary>
/// Endpoint for verifying user email addresses.
/// </summary>
public class VerifyEmail(IMediator mediator) : Endpoint<VerifyEmailRequest, Results<Ok<string>, ProblemHttpResult>>
{
    public override void Configure()
    {
        Post("auth/verify-email");
        AllowAnonymous();
        Summary(s =>
        {
            s.Summary = "Verify email address";
            s.Description = "Verifies a user's email address using a verification token.";
            s.Responses[200] = "Email has been successfully verified. Returns the user ID.";
            // Every token failure - unknown, expired, already used - answers 400 with the same
            // shape on purpose, so the response cannot be used to probe token or account state.
            s.Responses[400] = "Invalid or expired verification token.";
            s.ExampleRequest = new VerifyEmailRequest("abc123def456...");
            s.ResponseExamples[200] = "1";
        });
        Description(builder => builder
            .Produces<string>(StatusCodes.Status200OK, "application/json")
            .ProducesProblem(StatusCodes.Status400BadRequest));
    }

    /// <inheritdoc/>
    public override async Task<Results<Ok<string>, ProblemHttpResult>> ExecuteAsync(VerifyEmailRequest request, CancellationToken ct)
    {
        var verifyEmailCommand = new VerifyEmailCommand(request.Token);
        var emailVerificationResult = await mediator.Send(verifyEmailCommand, ct);

        var builder = TypedResultsBuilder.MapResult(emailVerificationResult, user => user.Id.ToString());

        // Only override the problem title for validation failures. Other statuses (404 in
        // particular) keep ToProblem's status-derived title instead of a 400-shaped message.
        if (emailVerificationResult.Status == Core.Infrastructure.Result.ResultStatus.Invalid)
        {
            var validationMessage = emailVerificationResult.ValidationErrors
                .Select(error => error.ErrorMessage)
                .FirstOrDefault(message => !string.IsNullOrWhiteSpace(message));

            builder.SetErrorMessage(validationMessage ?? "Please check the verification token and try again.");
        }

        return builder.SetTypedResults<Ok<string>, ProblemHttpResult>();
    }
}
