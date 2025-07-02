using Endatix.Core;
using Endatix.Core.Abstractions;
using Endatix.Core.Configuration;
using Microsoft.Extensions.Options;

namespace Endatix.Infrastructure.Email;

/// <summary>
/// Implements email template operations using configuration settings.
/// </summary>
public class EmailTemplateService : IEmailTemplateService
{
    private readonly IOptions<EmailTemplateSettings> _emailTemplateSettings;

    /// <summary>
    /// Initializes a new instance of the EmailTemplateService class.
    /// </summary>
    /// <param name="emailTemplateSettings">Email template configuration settings.</param>
    public EmailTemplateService(IOptions<EmailTemplateSettings> emailTemplateSettings)
    {
        _emailTemplateSettings = emailTemplateSettings;
    }

    /// <inheritdoc />
    public EmailWithTemplate CreateVerificationEmail(string userEmail, string token)
    {
        return new EmailWithTemplate
        {
            To = userEmail,
            From = _emailTemplateSettings.Value.EmailVerification.FromAddress,
            Subject = string.Empty, // Taken from the template
            TemplateId = _emailTemplateSettings.Value.EmailVerification.TemplateId,
            Metadata = new Dictionary<string, object>
            {
                ["hubUrl"] = _emailTemplateSettings.Value.HubUrl,
                ["verificationToken"] = token
            }
        };
    }
} 