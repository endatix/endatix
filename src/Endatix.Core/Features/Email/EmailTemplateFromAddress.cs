using Endatix.Core.Configuration;
using Endatix.Core.Entities;

namespace Endatix.Core.Features.Email;

/// <summary>
/// Temporary config overlay for template sender addresses until Hub UI/API
/// persist FromAddress on the database row. Delete this type and its call sites
/// when that ships. Kept public so Infrastructure can share the same resolve
/// path without InternalsVisibleTo.
/// </summary>
/// <remarks>
/// Precedence: explicit <c>Endatix:EmailTemplates:*:FromAddress</c> (customer
/// override without a DB edit) → the deployment sender's configured address
/// (<c>Endatix:Integrations:Email:Sender:Address</c>) → database FromAddress →
/// the deployment sender's address (a hand-edited row may hold an empty one).
/// </remarks>
public static class EmailTemplateFromAddress
{
    /// <summary>
    /// Resolves the sender that list and send both use.
    /// </summary>
    /// <param name="settings">The per-template sender overrides.</param>
    /// <param name="template">The template row.</param>
    /// <param name="sender">The deployment sender.</param>
    public static string Resolve(
        EmailTemplateSettings settings,
        EmailTemplate template,
        DeploymentSender sender)
    {
        var configuredFromAddress = GetConfiguredFromAddress(settings, template.Name);
        if (!string.IsNullOrWhiteSpace(configuredFromAddress))
        {
            return configuredFromAddress;
        }

        if (sender.ConfiguredAddress is not null)
        {
            return sender.ConfiguredAddress;
        }

        // Never an empty From: Mailgun and SendGrid refuse one.
        return string.IsNullOrWhiteSpace(template.FromAddress) ? sender.Address : template.FromAddress;
    }

    private static string? GetConfiguredFromAddress(EmailTemplateSettings settings, string templateName)
    {
        if (string.Equals(templateName, settings.EmailVerification.TemplateId, StringComparison.Ordinal))
        {
            return settings.EmailVerification.FromAddress;
        }

        if (IsAdminInviteTemplate(templateName, settings.UserInvitation.TemplateId))
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

    private static bool IsAdminInviteTemplate(string templateName, string configuredTemplateId)
    {
        return string.Equals(templateName, configuredTemplateId, StringComparison.OrdinalIgnoreCase)
            || string.Equals(
                templateName,
                EmailTemplateSettings.UserInvitationTemplateId,
                StringComparison.OrdinalIgnoreCase);
    }
}
