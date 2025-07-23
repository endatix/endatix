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
    /// The template Id that will be used for sending the Contact Us Response email
    /// </summary>
    [Required]
    public required string ContactUsResponseTemplateId { get; set; }

    /// <summary>
    /// The email address from where the system email notifications will be sent on successful Contact Us form submission
    /// </summary>
    [EmailAddress]
    [Required]
    public required string NotificationEmailFrom { get; set; }

    /// <summary>
    /// The email address to where the system email notifications will be sent on successful Contact Us form submission
    /// </summary>
    [EmailAddress]
    [Required]
    public required string NotificationEmailTo { get; set; }
}
