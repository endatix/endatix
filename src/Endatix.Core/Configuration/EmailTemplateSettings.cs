namespace Endatix.Core.Configuration;

/// <summary>
/// Configuration for email template settings.
/// </summary>
public class EmailTemplateSettings
{
    /// <summary>
    /// The base URL for Endatix Hub application.
    /// </summary>
    public string HubUrl { get; set; } = string.Empty;

    /// <summary>
    /// Email verification template settings.
    /// </summary>
    public EmailTemplateConfig EmailVerification { get; set; } = new();

    /// <summary>
    /// Welcome email template settings.
    /// </summary>
    public EmailTemplateConfig WelcomeEmail { get; set; } = new();

    /// <summary>
    /// Forgot password email template settings.
    /// </summary>
    public EmailTemplateConfig ForgotPasswordEmail { get; set; } = new();

    /// <summary>
    /// Reset password email template settings.
    /// </summary>
    public EmailTemplateConfig ResetPasswordEmail { get; set; } = new();
}

/// <summary>
/// Configuration for a specific email template.
/// </summary>
public class EmailTemplateConfig
{
    /// <summary>
    /// Template ID or name (SendGrid template ID or SMTP template name).
    /// </summary>
    public string TemplateId { get; set; } = string.Empty;

    /// <summary>
    /// From email address.
    /// </summary>
    public string FromAddress { get; set; } = string.Empty;
}