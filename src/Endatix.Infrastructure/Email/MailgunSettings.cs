using System.ComponentModel.DataAnnotations;

namespace Endatix.Infrastructure.Email;

/// <summary>
/// POCO Class for the Mailgun per environment settings needed.
/// </summary>
public class MailgunSettings
{
    /// <summary>
    /// The ApiKey provided by Mailgun
    /// </summary>
    [Required]
    public string ApiKey { get; set; }


    /// <summary>
    /// The Base Url for the Mailgun, e.g. https://api.mailgun.net/v3/
    /// </summary>
    [Required]
    public string BaseUrl { get; set; }


    /// <summary>
    /// The domain configured in your Mailgun
    /// </summary>
    [Required]
    public string Domain { get; set; }
}