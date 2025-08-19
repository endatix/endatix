using System.Text;
using System.Text.Encodings.Web;
using Ardalis.GuardClauses;
using Endatix.Core;
using Endatix.Core.Abstractions;
using Endatix.Core.Configuration;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Options;

namespace Endatix.Infrastructure.Email;

/// <summary>
/// Implements email template operations using configuration settings.
/// </summary>
public class EmailTemplateService : IEmailTemplateService
{
    private const string DEFAULT_FROM_ADDRESS = "noreply@endatix.com";

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

    /// <inheritdoc />
    public EmailWithTemplate CreateForgotPasswordEmail(string userEmail, string token)
    {
        Guard.Against.NullOrWhiteSpace(userEmail);
        Guard.Against.NullOrWhiteSpace(token);

        var resetCode = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(token));
        var query = $"email={HtmlEncoder.Default.Encode(userEmail)}&resetCode={HtmlEncoder.Default.Encode(resetCode)}";
        return new EmailWithTemplate
        {
            To = userEmail,
            From = _emailTemplateSettings.Value.ForgotPasswordEmail.FromAddress ?? DEFAULT_FROM_ADDRESS,
            TemplateId = _emailTemplateSettings.Value.ForgotPasswordEmail.TemplateId,
            Metadata = new Dictionary<string, object>
            {
                ["hubUrl"] = _emailTemplateSettings.Value.HubUrl,
                ["resetCodeQuery"] = query
            }
        };
    }
}