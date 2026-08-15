namespace Endatix.Core.Configuration;

/// <summary>
/// Configuration for email template settings.
/// </summary>
/// <remarks>
/// Two account-activation emails exist:
/// <list type="bullet">
/// <item>
/// <see cref="EmailVerification"/> — self-service registration (create-account).
/// Sends a verify-email link. Productized later; the send path already exists.
/// </item>
/// <item>
/// <see cref="TenantInvitation"/> — admin invite-to-tenant (shipped).
/// Sends an activate-invite link so the invited user can set a password.
/// </item>
/// </list>
/// <see cref="UserInvitation"/> currently aliases <see cref="TenantInvitation"/> so
/// existing <c>Endatix:EmailTemplates:UserInvitation</c> hosts keep working.
/// When self-service needs its own invitation template, split that property
/// without changing the admin-invite key.
/// <para>
/// <see cref="EmailTemplateConfig.FromAddress"/> is the supported way for hosts to
/// change the sender without a database edit, until Hub UI/API persist FromAddress
/// on the template row.
/// </para>
/// </remarks>
public class EmailTemplateSettings
{
    /// <summary>
    /// Seeded database template name for the admin invite-to-tenant email.
    /// Named historically for the self-service invitation plan; the row is
    /// used by <see cref="TenantInvitation"/>.
    /// </summary>
    public const string UserInvitationTemplateId = "user-invitation";

    /// <summary>
    /// Template id previously used in some host configs for the admin invite
    /// email. Maps to <see cref="UserInvitationTemplateId"/>.
    /// </summary>
    public const string LegacyTenantInvitationTemplateId = "tenant-invitation";

    /// <summary>
    /// The base URL for Endatix Hub application.
    /// </summary>
    [Obsolete("Use Endatix:Hub:HubBaseUrl instead")]
    public string HubUrl { get; set; } = string.Empty;

    /// <summary>
    /// Self-service registration verification email.
    /// Canonical config key: <c>Endatix:EmailTemplates:EmailVerification</c>.
    /// </summary>
    public EmailTemplateConfig EmailVerification { get; set; } = new()
    {
        TemplateId = "email-verification"
    };

    /// <summary>
    /// Admin invite-to-tenant activation email.
    /// Canonical config key: <c>Endatix:EmailTemplates:TenantInvitation</c>.
    /// </summary>
    public EmailTemplateConfig TenantInvitation { get; set; } = new()
    {
        TemplateId = UserInvitationTemplateId
    };

    /// <summary>
    /// Compatibility alias for <see cref="TenantInvitation"/>.
    /// Kept so <c>Endatix:EmailTemplates:UserInvitation</c> continues to bind.
    /// Reserved to become a distinct self-service invitation template later.
    /// </summary>
    public EmailTemplateConfig UserInvitation
    {
        get => TenantInvitation;
        set
        {
            if (value is not null)
            {
                TenantInvitation = value;
            }
        }
    }

    /// <summary>
    /// Welcome email template settings.
    /// </summary>
    public EmailTemplateConfig WelcomeEmail { get; set; } = new();

    /// <summary>
    /// Forgot password email template settings.
    /// </summary>
    public EmailTemplateConfig ForgotPasswordEmail { get; set; } = new()
    {
        TemplateId = "forgot-password"
    };

    /// <summary>
    /// Password changed email template settings.
    /// </summary>
    public EmailTemplateConfig PasswordChangedEmail { get; set; } = new()
    {
        TemplateId = "password-changed"
    };

    /// <summary>
    /// Maps the legacy <c>tenant-invitation</c> template id to the seeded
    /// <see cref="UserInvitationTemplateId"/> so send and list stay aligned.
    /// </summary>
    public void NormalizeInvitationTemplate()
    {
        if (string.IsNullOrWhiteSpace(TenantInvitation.TemplateId)
            || string.Equals(
                TenantInvitation.TemplateId,
                LegacyTenantInvitationTemplateId,
                StringComparison.OrdinalIgnoreCase))
        {
            TenantInvitation.TemplateId = UserInvitationTemplateId;
        }
    }
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
    /// Optional sender override. When set, list and send use this address instead
    /// of the database row — so hosts can change the sender without a DB edit.
    /// Leave empty to use the seeded (or UI-persisted) database FromAddress.
    /// </summary>
    public string FromAddress { get; set; } = string.Empty;
}
