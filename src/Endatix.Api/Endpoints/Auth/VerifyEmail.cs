using FastEndpoints;
using MediatR;
using Microsoft.AspNetCore.Http.HttpResults;
using Errors = Microsoft.AspNetCore.Mvc;
using Endatix.Core.UseCases.Identity.VerifyEmail;
using Endatix.Api.Infrastructure;

namespace Endatix.Api.Endpoints.Auth;

/// <summary>
/// Endpoint for verifying user email addresses.
/// </summary>
public class VerifyEmail(IMediator mediator) : Endpoint<VerifyEmailRequest, Results<Ok<string>, BadRequest<Errors.ProblemDetails>, NotFound>>
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
            s.Responses[400] = "Invalid or expired verification token.";
            s.Responses[404] = "Verification token not found.";
            s.ExampleRequest = new VerifyEmailRequest("abc123def456...");
        });
    }

    /// <inheritdoc/>
    public override async Task<Results<Ok<string>, BadRequest<Errors.ProblemDetails>, NotFound>> ExecuteAsync(VerifyEmailRequest request, CancellationToken cancellationToken)
    {
        var verifyEmailCommand = new VerifyEmailCommand(request.Token);
        var emailVerificationResult = await mediator.Send(verifyEmailCommand, cancellationToken);

        return TypedResultsBuilder
                .MapResult(emailVerificationResult, user => user.Id.ToString())
                .SetTypedResults<Ok<string>, BadRequest<Errors.ProblemDetails>, NotFound>();
    }
} 