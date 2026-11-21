using MimeKit;

namespace Endatix.Infrastructure.Email;

/// <summary>
/// Builds the From mailbox the providers send. The From may already carry a display name (an operator's
/// <c>Endatix:EmailTemplates:*:FromAddress</c> written as <c>Name &lt;address&gt;</c>), so it is parsed as a mailbox
/// first, then named; building a mailbox from it as a bare address would throw.
/// </summary>
internal static class FromMailbox
{
    /// <summary>
    /// The mailbox of <paramref name="from"/>, named <paramref name="name"/> when that is set, else by the name it
    /// already carries, else <paramref name="fallbackName"/>.
    /// </summary>
    internal static MailboxAddress Create(string from, string? name, string? fallbackName = null)
    {
        var mailbox = MailboxAddress.Parse(from);
        if (!string.IsNullOrWhiteSpace(name))
        {
            mailbox.Name = name;
        }
        else if (string.IsNullOrWhiteSpace(mailbox.Name))
        {
            mailbox.Name = fallbackName;
        }

        return mailbox;
    }
}
