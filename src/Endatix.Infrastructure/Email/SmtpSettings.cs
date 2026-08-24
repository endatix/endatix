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
    /// Explicitly controls how the connection is secured. Defaults to <see cref="SmtpSecurityMode.Auto"/>,
    /// which derives the mode from <see cref="EnableSsl"/> and <see cref="Port"/> (implicit TLS on port
    /// 465, STARTTLS otherwise). Set this to override the derived choice, e.g. to force
    /// <see cref="SmtpSecurityMode.SslOnConnect"/> on a non-standard port.
    /// </summary>
    public SmtpSecurityMode SecurityMode { get; set; } = SmtpSecurityMode.Auto;

    /// <summary>
    /// Whether to check the SMTP server's certificate for revocation during the TLS handshake.
    /// Defaults to <c>false</c> to match the behavior of the previous System.Net.Mail-based sender,
    /// since some networks block the OCSP/CRL endpoints needed for the check, which would otherwise
    /// fail the connection. Set to <c>true</c> for stricter certificate validation.
    /// </summary>
    public bool CheckCertificateRevocation { get; set; } = false;

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