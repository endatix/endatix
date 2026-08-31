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
/// <see cref="UserInvitation"/> — admin invite (shipped).
/// Sends an activate-invite link so the invited user can set a password.
/// Also used when waitlist approval creates a new user.
/// </item>
/// </list>
/// Waitlist templates <see cref="TenantSignupRequestTemplateId"/> (platform admins)
/// and <see cref="TenantSignupApprovedTemplateId"/> (existing users after approval)
/// are seeded in AppEntities; they are not account-activation emails.
/// <para>
/// <see cref="EmailTemplateConfig.FromAddress"/> is the supported way for hosts to
/// change the sender without a database edit, until Hub UI/API persist FromAddress
/// on the template row.
/// </para>
/// </remarks>
public class EmailTemplateSettings
{
    /// <summary>
    /// Seeded database template name for the admin invite email.
    /// Hub lists this name; <see cref="UserInvitation"/> is the config key.
    /// </summary>
    public const string UserInvitationTemplateId = "user-invitation";

    /// <summary>
    /// Seeded database template name for platform-admin waitlist notifications.
    /// </summary>
    public const string TenantSignupRequestTemplateId = "tenant-signup-request";

    /// <summary>
    /// Seeded database template name for existing users after waitlist approval.
    /// New users receive <see cref="UserInvitation"/> instead.
    /// </summary>
    public const string TenantSignupApprovedTemplateId = "tenant-signup-approved";

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
    /// User invitation activation email.
    /// Canonical config key: <c>Endatix:EmailTemplates:UserInvitation</c>.
    /// </summary>
    public EmailTemplateConfig UserInvitation { get; set; } = new()
    {
        TemplateId = UserInvitationTemplateId
    };

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
