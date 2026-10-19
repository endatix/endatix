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
}
