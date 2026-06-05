using FastEndpoints;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Endatix.Api.Infrastructure;
using Endatix.Core.UseCases.Email.SendTestEmail;
using Endatix.Core.Abstractions.Authorization;
using FluentValidation;

namespace Endatix.Api.Endpoints.Admin.Email;

/// <summary>
/// Endpoint for sending a test email to verify email provider configuration.
/// </summary>
public class SendTestEmail(IMediator mediator)
    : Endpoint<SendTestEmailRequest, Results<Ok<string>, ProblemHttpResult>>
{
    /// <summary>
    /// Configures the endpoint.
    /// </summary>
    public override void Configure()
    {
        Post("/admin/email/test");
        Policies(SystemRole.PlatformAdmin.Name);
        Summary(s =>
        {
            s.Summary = "Send test email";
            s.Description = "Sends a test email to verify email provider configuration.";
            s.ExampleRequest = new SendTestEmailRequest(
                "admin@example.com",
                "noreply@example.com",
                null);
            s.ResponseExamples[200] = "Test email sent successfully.";
            s.Responses[200] = "Test email sent successfully.";
            s.Responses[400] = "Invalid input data.";
        });
        Description(builder => builder
            .Produces<string>(200, "application/json")
            .ProducesProblem(400));
    }

    /// <inheritdoc/>
    public override async Task<Results<Ok<string>, ProblemHttpResult>> ExecuteAsync(SendTestEmailRequest request, CancellationToken ct)
    {
        var command = new SendTestEmailCommand(
            request.ToEmail,
            request.FromEmail,
            request.TemplateId);
        var result = await mediator.Send(command, ct);

        return TypedResultsBuilder
            .MapResult(result, _ => "Test email sent successfully.")
            .SetTypedResults<Ok<string>, ProblemHttpResult>();
    }
}

/// <summary>
/// Validator for the SendTestEmailRequest used in the email test sending process.
/// </summary>
public class SendTestEmailValidator : Validator<SendTestEmailRequest>
{
    /// <summary>
    /// Initializes a new instance of the SendTestEmailValidator class.
    /// </summary>
    public SendTestEmailValidator()
    {
        RuleFor(x => x.ToEmail)
            .NotEmpty()
            .EmailAddress();

        RuleFor(x => x.FromEmail)
            .NotEmpty()
            .EmailAddress();
    }
}

/// <summary>
/// Request model for sending a test email.
/// </summary>
/// <param name="ToEmail">The email address to send the test email to.</param>
/// <param name="FromEmail">The email address to send the test email from.</param>
/// <param name="TemplateId">The ID of the email template to send.</param>
public record SendTestEmailRequest(
    string ToEmail,
    string FromEmail,
    string? TemplateId = null);
