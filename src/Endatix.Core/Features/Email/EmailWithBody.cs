using System.Diagnostics.CodeAnalysis;
using Endatix.Core.Abstractions;

namespace Endatix.Core.Features.Email;

/// <summary>
/// Emails that have PlainText and HTML Body defined
/// </summary>
public class EmailWithBody : BaseEmailModel
{
    /// <summary>
    /// Creates an email whose plain-text and HTML bodies start empty.
    /// </summary>
    public EmailWithBody()
    {
        PlainTextBody = string.Empty;
        HtmlBody = string.Empty;
    }

    /// <summary>
    /// Plain text version of the body
    /// </summary>
    public string PlainTextBody { get; set; }

    /// <summary>
    /// Html version of the body
    /// </summary>
    public string HtmlBody { get; set; }

    /// <summary>
    /// The display name on the From header. A rendered template sets the deployment sender's name. Null leaves
    /// each provider's own behaviour: SMTP sends the deployment sender's name, Mailgun and SendGrid the bare address.
    /// </summary>
    [Experimental("ENDATIX0004")]
    public string? FromName { get; set; }
}
