using System.ComponentModel.DataAnnotations;

namespace Endatix.Infrastructure.Email;

/// <summary>
/// POCO Class for the SendGrid per environment settings needed.
/// </summary>
public class SendGridSettings
{

    /// <summary>
    /// The SendGrid API key that will be used for the API requests
    /// </summary>
    [Required]
    public string ApiKey { get; set; }
}