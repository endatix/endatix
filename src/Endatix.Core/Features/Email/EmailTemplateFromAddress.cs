using Endatix.Core.Configuration;

namespace Endatix.Core.Features.Email;

/// <summary>
/// Resolves the effective sender address for database-backed email templates.
/// </summary>
public static class EmailTemplateFromAddress
{
    /// <summary>
    /// The default sender address used when no configuration or database value is available.
    /// </summary>
    public const string DefaultFromAddress = "noreply@endatix.com";

    /// <summary>
    /// Resolves the sender address that will be used when sending a template email.
    /// Configuration overrides take precedence over the database value.
    /// </summary>
    /// <param name="settings">The email template configuration.</param>
    /// <param name="templateName">The database template name.</param>
    /// <param name="databaseFromAddress">The sender address stored on the template.</param>
    /// <returns>The effective sender address.</returns>
    public static string ResolveEffectiveFromAddress(
        EmailTemplateSettings settings,
        string templateName,
        string databaseFromAddress)
    {
        var configuredFromAddress = GetConfiguredFromAddress(settings, templateName);
        if (!string.IsNullOrEmpty(configuredFromAddress))
        {
            return configuredFromAddress;
        }

        if (!string.IsNullOrEmpty(databaseFromAddress))
        {
            return databaseFromAddress;
        }

        return DefaultFromAddress;
    }

    /// <summary>
    /// Returns the configured sender address for a template when one is defined.
    /// </summary>
    /// <param name="settings">The email template configuration.</param>
    /// <param name="templateName">The database template name.</param>
    /// <returns>The configured sender address, or null when no mapping exists.</returns>
    public static string? GetConfiguredFromAddress(EmailTemplateSettings settings, string templateName)
    {
        if (string.Equals(templateName, settings.EmailVerification.TemplateId, StringComparison.Ordinal))
        {
            return settings.EmailVerification.FromAddress;
        }

        if (string.Equals(templateName, settings.UserInvitation.TemplateId, StringComparison.Ordinal))
        {
            return settings.UserInvitation.FromAddress;
        }

        if (string.Equals(templateName, settings.ForgotPasswordEmail.TemplateId, StringComparison.Ordinal))
        {
            return settings.ForgotPasswordEmail.FromAddress;
        }

        if (string.Equals(templateName, settings.PasswordChangedEmail.TemplateId, StringComparison.Ordinal))
        {
            return settings.PasswordChangedEmail.FromAddress;
        }

        if (string.Equals(templateName, settings.WelcomeEmail.TemplateId, StringComparison.Ordinal))
        {
            return settings.WelcomeEmail.FromAddress;
        }

        return null;
    }
}
