using Endatix.Core.Features.Email;
using Endatix.Core.Infrastructure.Logging;
using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Result;
using Microsoft.Extensions.Logging;

namespace Endatix.Core.UseCases.Email.SendTestEmail;

/// <summary>
/// Handler for the SendTestEmailCommand.
/// </summary>
/// <param name="emailSender">The email sender.</param>
/// <param name="logger">The logger.</param>
public partial class SendTestEmailHandler(
    IEmailSender emailSender,
    ILogger<SendTestEmailHandler> logger
) : ICommandHandler<SendTestEmailCommand, Result>
{
    /// <summary>
    /// Handles the SendTestEmailCommand.
    /// </summary>
    /// <param name="request">The SendTestEmailCommand.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The result.</returns>
    public async Task<Result> Handle(SendTestEmailCommand request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.ToEmail))
        {
            return Result.Invalid(new ValidationError("Email address cannot be empty."));
        }

        var fromAddress = "noreply@endatix.com";

        if (request.TemplateId is not null)
        {
            var emailWithTemplate = new EmailWithTemplate
            {
                To = request.ToEmail,
                From = fromAddress,
                TemplateId = request.TemplateId,
                Subject = "Test Email from Endatix",
                Metadata = new Dictionary<string, object>
                {
                    { "name", "Admin Test User" },
                    { "activationUrl", "https://endatix.com/test" }
                }
            };

            await emailSender.SendEmailAsync(emailWithTemplate, cancellationToken);
        }
        else
        {
            var emailWithBody = new EmailWithBody
            {
                To = request.ToEmail,
                From = fromAddress,
                Subject = "Test Email from Endatix",
                PlainTextBody = "This is a test email sent from the Endatix admin panel.",
                HtmlBody = "<h1>Test Email</h1><p>This is a test email sent from the Endatix admin panel.</p>"
            };

            await emailSender.SendEmailAsync(emailWithBody, cancellationToken);
        }

        LogTestEmailSent(logger, SensitiveValue.Email(request.ToEmail));

        return Result.SuccessWithMessage("Test email sent successfully.");
    }

    [LoggerMessage(
        EventId = 52001,
        Level = LogLevel.Information,
        EventName = "TestEmailSent",
        Message = "Test email sent successfully to {Email}")]
    private static partial void LogTestEmailSent(ILogger logger, SensitiveValue email);
}
