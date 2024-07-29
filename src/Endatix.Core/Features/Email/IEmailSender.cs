using System.Threading;
using System.Threading.Tasks;

namespace Endatix.Core.Features.Email;

/// <summary>
/// Interface for defining email sending capabilities
/// </summary>
public interface IEmailSender
{
    /// <summary>
    /// Send email async passing the body of the email
    /// </summary>
    /// <param name="email">Email model of type <see cref="EmailWithBody"/> </param>
    /// <param name="cancellationToken"></param>
    /// <returns>Task</returns>
    Task SendEmailAsync(EmailWithBody email, CancellationToken cancellationToken = default);

    /// <summary>
    /// Send email async passing an Id of the email template that will be used
    /// </summary>
    /// <param name="email">Email model of type <see cref="EmailWithTemplate"/> </param>
    /// <param name="cancellationToken"></param>
    /// <returns>Task</returns>
    Task SendEmailAsync(EmailWithTemplate email, CancellationToken cancellationToken = default);
}