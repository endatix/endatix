using Endatix.Core.Configuration;

namespace Endatix.Core.Features.Email;

/// <summary>
/// Temporary config overlay for template sender addresses until Hub UI/API
/// persist FromAddress on the database row. Delete this type and its call sites
/// when that ships. Kept public so Infrastructure can share the same resolve
/// path without InternalsVisibleTo.
/// </summary>
/// <remarks>
/// Precedence: explicit <c>Endatix:EmailTemplates:*:FromAddress</c> (customer
/// override without a DB edit) → the brand pack's <c>email.fromAddress</c> → database FromAddress
/// → <see cref="DefaultFromAddress"/>.
/// </remarks>
public static class EmailTemplateFromAddress
{
    /// <summary>
    /// Fallback when neither config nor the database row has a sender.
    /// </summary>
    public const string DefaultFromAddress = "noreply@endatix.com";

    /// <summary>
    /// Resolves the sender that list and send both use.
    /// </summary>
    public static string Resolve(
        EmailTemplateSettings settings,
        string templateName,
        string databaseFromAddress) =>
        Resolve(settings, templateName, databaseFromAddress, brandFromAddress: null);

    /// <summary>
    /// Resolves the sender with a brand pack's address between the operator's per-template config and the
    /// database row: config → <paramref name="brandFromAddress"/> → database → <see cref="DefaultFromAddress"/>.
    /// </summary>
    /// <param name="settings">The bound email template settings.</param>
    /// <param name="templateName">The template row's name.</param>
    /// <param name="databaseFromAddress">The template row's sender.</param>
    /// <param name="brandFromAddress">The brand pack's <c>email.fromAddress</c>; null for the built-in brand.</param>
    public static string Resolve(
        EmailTemplateSettings settings,
        string templateName,
        string databaseFromAddress,
        string? brandFromAddress)
    {
        var configuredFromAddress = GetConfiguredFromAddress(settings, templateName);
        if (!string.IsNullOrWhiteSpace(configuredFromAddress))
        {
            return configuredFromAddress;
        }

        if (!string.IsNullOrWhiteSpace(brandFromAddress))
        {
            return brandFromAddress;
        }

        if (!string.IsNullOrWhiteSpace(databaseFromAddress))
        {
            return databaseFromAddress;
        }

        return DefaultFromAddress;
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
