using FastEndpoints;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Endatix.Core.UseCases.Identity.SendVerificationEmail;
using Endatix.Api.Infrastructure;

namespace Endatix.Api.Endpoints.Auth;

/// <summary>
/// Endpoint for sending verification emails to users.
/// </summary>
public class SendVerificationEmail(IMediator mediator) : Endpoint<SendVerificationEmailRequest, Results<Ok<string>, ProblemHttpResult>>
{
    public override void Configure()
    {
        Post("auth/send-verification-email");
        AllowAnonymous();
        Summary(s =>
        {
            s.Summary = "Send verification email";
            s.Description = "Sends a verification email to the specified email address if the user exists and is not already verified.";
            s.Responses[200] = "Verification email has been sent successfully.";
            s.Responses[400] = "Invalid email address.";
            s.ExampleRequest = new SendVerificationEmailRequest("user@example.com");
            s.ResponseExamples[200] = "Verification email sent successfully";
        });
        Description(builder => builder
            .Produces<string>(StatusCodes.Status200OK, "application/json")
            .ProducesProblem(StatusCodes.Status400BadRequest));
    }

    /// <inheritdoc/>
    public override async Task<Results<Ok<string>, ProblemHttpResult>> ExecuteAsync(SendVerificationEmailRequest request, CancellationToken ct)
    {
        var sendVerificationEmailCommand = new SendVerificationEmailCommand(request.Email);
        var result = await mediator.Send(sendVerificationEmailCommand, ct);

        return TypedResultsBuilder
                .MapResult(result, result => "Verification email sent successfully")
                .SetTypedResults<Ok<string>, ProblemHttpResult>();
    }
}
