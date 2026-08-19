namespace Endatix.Infrastructure.Email;

/// <summary>
/// Controls how <see cref="SmtpEmailSender"/> secures its connection to the SMTP server.
/// </summary>
public enum SmtpSecurityMode
{
    /// <summary>
    /// Derive the mode from <see cref="SmtpSettings.EnableSsl"/> and <see cref="SmtpSettings.Port"/>:
    /// disabled when EnableSsl is false, implicit TLS on port 465, STARTTLS otherwise.
    /// Matches the pre-MailKit SmtpClient-based sender for every configuration it could already
    /// reach; port 465 is new behavior — System.Net.Mail could not do implicit TLS at all.
    /// </summary>
    Auto = 0,

    /// <summary>No transport security.</summary>
    None,

    /// <summary>Require STARTTLS; fail if the server does not support it.</summary>
    StartTls,

    /// <summary>Use STARTTLS if the server advertises support for it, otherwise connect in plain text.</summary>
    StartTlsWhenAvailable,

    /// <summary>Use implicit TLS (SMTPS), typically on port 465.</summary>
    SslOnConnect
}
