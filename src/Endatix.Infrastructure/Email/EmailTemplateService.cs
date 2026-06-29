using System.Text;
using Ardalis.GuardClauses;
using Endatix.Core;
using Endatix.Core.Abstractions;
using Endatix.Core.Configuration;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Options;

namespace Endatix.Infrastructure.Email;

/// <summary>
/// Implements email template operations using configuration settings.
/// Hub URL is read from HubSettings (canonical); falls back to EmailTemplateSettings.HubUrl for backward compatibility.
/// </summary>
public class EmailTemplateService : IEmailTemplateService
{
    private const string DEFAULT_FROM_ADDRESS = "noreply@endatix.com";

    private readonly IOptions<EmailTemplateSettings> _emailTemplateSettings;
    private readonly IOptions<HubSettings> _hubSettings;

    /// <summary>
    /// Initializes a new instance of the EmailTemplateService class.
    /// </summary>
    public EmailTemplateService(IOptions<EmailTemplateSettings> emailTemplateSettings, IOptions<HubSettings> hubSettings)
    {
        _emailTemplateSettings = emailTemplateSettings;
        _hubSettings = hubSettings;
    }

    private string HubUrl => !string.IsNullOrWhiteSpace(_hubSettings.Value.HubBaseUrl)
        ? _hubSettings.Value.HubBaseUrl.Trim()
        : _emailTemplateSettings.Value.HubUrl ?? string.Empty;

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
                ["hubUrl"] = HubUrl,
                ["verificationToken"] = token,
                ["verificationUrl"] = token is not null
                    ? BuildHubUrl("verify-email", new Dictionary<string, string?> { ["token"] = token })
                    : string.Empty,
                ["headline"] = "Verify your email address",
                ["bodyText"] = "Verify your email address to activate your Endatix account.",
                ["actionText"] = "Verify email address"
            }
        };
    }

    /// <inheritdoc />
    public EmailWithTemplate CreateInvitationEmail(string userEmail, string token)
    {
        return new EmailWithTemplate
        {
            To = userEmail,
            From = _emailTemplateSettings.Value.UserInvitation.FromAddress,
            Subject = string.Empty, // Taken from the template
            TemplateId = _emailTemplateSettings.Value.UserInvitation.TemplateId,
            Metadata = new Dictionary<string, object>
            {
                ["hubUrl"] = HubUrl,
                ["verificationToken"] = token,
                ["activationUrl"] = token is not null
                    ? BuildHubUrl("activate-invite", new Dictionary<string, string?> { ["token"] = token })
                    : string.Empty,
                ["headline"] = "Accept your Endatix invitation",
                ["bodyText"] = "Set your password to activate your account and sign in to Endatix Hub.",
                ["actionText"] = "Accept invitation"
            }
        };
    }

    /// <inheritdoc />
    public EmailWithTemplate CreateForgotPasswordEmail(string userEmail, string token)
    {
        Guard.Against.NullOrWhiteSpace(userEmail);
        Guard.Against.NullOrWhiteSpace(token);

        var resetCode = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(token));
        var resetCodeQuery = BuildQueryString(new Dictionary<string, string?>
        {
            ["email"] = userEmail,
            ["resetCode"] = resetCode
        });
        return new EmailWithTemplate
        {
            To = userEmail,
            From = _emailTemplateSettings.Value.ForgotPasswordEmail.FromAddress ?? DEFAULT_FROM_ADDRESS,
            TemplateId = _emailTemplateSettings.Value.ForgotPasswordEmail.TemplateId,
            Metadata = new Dictionary<string, object>
            {
                ["hubUrl"] = HubUrl,
                ["resetCodeQuery"] = resetCodeQuery
            }
        };
    }

    /// <inheritdoc />
    public EmailWithTemplate CreatePasswordChangedEmail(string userEmail)
    {
        Guard.Against.NullOrWhiteSpace(userEmail);

        return new EmailWithTemplate
        {
            To = userEmail,
            From = _emailTemplateSettings.Value.PasswordChangedEmail.FromAddress ?? DEFAULT_FROM_ADDRESS,
            TemplateId = _emailTemplateSettings.Value.PasswordChangedEmail.TemplateId,
            Metadata = []
        };
    }

    private string BuildHubUrl(string path, Dictionary<string, string?> queryParams)
    {
        var url = $"{HubUrl.TrimEnd('/')}/{path.TrimStart('/')}";
        return QueryHelpers.AddQueryString(url, queryParams);
    }

    private static string BuildQueryString(Dictionary<string, string?> queryParams)
    {
        var query = QueryHelpers.AddQueryString(string.Empty, queryParams);
        return query.TrimStart('?');
    }
}