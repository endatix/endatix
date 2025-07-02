using System.ComponentModel.DataAnnotations;

namespace Endatix.Infrastructure.Email;

/// <summary>
/// POCO Class for SMTP settings needed for email sending.
/// </summary>
public class SmtpSettings
{
    /// <summary>
    /// The SMTP server host name or IP address.
    /// </summary>
    [Required]
    public string Host { get; set; } = "localhost";

    /// <summary>
    /// The SMTP server port number.
    /// </summary>
    public int Port { get; set; } = 587;

    /// <summary>
    /// Whether to enable SSL/TLS encryption.
    /// </summary>
    public bool EnableSsl { get; set; } = true;

    /// <summary>
    /// The username for SMTP authentication (optional).
    /// </summary>
    public string? Username { get; set; }

    /// <summary>
    /// The password for SMTP authentication (optional).
    /// </summary>
    public string? Password { get; set; }

    /// <summary>
    /// The default sender email address.
    /// </summary>
    [Required]
    [EmailAddress]
    public string DefaultFromAddress { get; set; } = "noreply@endatix.com";

    /// <summary>
    /// The default sender display name.
    /// </summary>
    public string DefaultFromName { get; set; } = "Endatix";
} 