using MimeKit;

namespace Endatix.Infrastructure.Email;

/// <summary>
/// Builds the From mailbox the providers send. The From may already carry a display name (an operator's
/// <c>Endatix:EmailTemplates:*:FromAddress</c> written as <c>Name &lt;address&gt;</c>), so it is parsed as a mailbox
/// first; building a mailbox from it as a bare address would throw. That name is the most specific setting, so it
/// is kept; a bare address gets the email's name.
/// </summary>
internal static class FromMailbox
{
    /// <summary>
    /// The mailbox of <paramref name="from"/>, by the name it already carries, else <paramref name="name"/>, else
    /// <paramref name="fallbackName"/>.
    /// </summary>
    internal static MailboxAddress Create(string from, string? name, string? fallbackName = null)
    {
        var mailbox = MailboxAddress.Parse(from);
        if (string.IsNullOrWhiteSpace(mailbox.Name))
        {
            mailbox.Name = string.IsNullOrWhiteSpace(name) ? fallbackName : name;
        }

        return mailbox;
    }
}
