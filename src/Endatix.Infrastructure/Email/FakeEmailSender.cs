using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Endatix.Core;
using Endatix.Core.Features.Email;
using Endatix.Core.Infrastructure.Logging;

namespace Endatix.Infrastructure.Email;

/// <summary>
/// Fake email sender useful for testing and debugging purposes. Not intended for Production for obvious reasons.
/// </summary>
/// <param name="logger"></param>
public class FakeEmailSender(ILogger<FakeEmailSender> logger) : IEmailSender
{
    public Task SendEmailAsync(EmailWithBody email, CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Not actually sending an email to {to} from {from} with subject {subject}", SensitiveValue.Email(email.To), email.From, email.Subject);
        return Task.CompletedTask;
    }

    public Task SendEmailAsync(EmailWithTemplate email, CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Not actually sending an email to {to} from {from} with subject {subject}", SensitiveValue.Email(email.To), email.From, email.Subject);
        return Task.CompletedTask;
    }
}
