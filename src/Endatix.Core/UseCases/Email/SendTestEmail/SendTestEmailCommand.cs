using Endatix.Core.Infrastructure.Logging;
using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Result;

namespace Endatix.Core.UseCases.Email.SendTestEmail;

/// <summary>
/// Command to send a test email.
/// </summary>
/// <param name="ToEmail">The email address to send the test email to.</param>
/// <param name="TemplateId">The ID of the email template to send.</param>
public record SendTestEmailCommand(
    [property: Sensitive(SensitivityType.Email)] string ToEmail,
    string? TemplateId = null
) : ICommand<Result>;
