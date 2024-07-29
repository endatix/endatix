using System.ComponentModel.DataAnnotations;

namespace Endatix.Samples.CustomEventHandlers;

/// <summary>
/// Config options related to the Contact Us form. Will be registered using the IOptions pattern
/// </summary>
public class ContactUsOptions
{
    /// <summary>
    /// Const for the AppSettings section key that will be used to store the Contact Us Form config options
    /// </summary>
    public const string CONFIG_SECTION_KEY = "Email:ContactUsForm";

    /// <summary>
    /// The "Welcome email" template Id that will be used for sending the Welcome email
    /// </summary>
    [Required]
    public required string WelcomeEmailTemplateId { get; set; }

    /// <summary>
    /// The email address on behalf of which "Welcome email" will be sent
    /// </summary>
    [EmailAddress]
    [Required]
    public required string WelcomeEmailFrom { get; set; }

    /// <summary>
    /// The email address where the system email notifications will be sent on successful contact us form submission
    /// </summary>
    [EmailAddress]
    [Required]
    public required string NotificationEmailTo { get; set; }
}
