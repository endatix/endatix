using Endatix.Core.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Endatix.Infrastructure.Email;

/// <summary>
/// The deprecated <see cref="SmtpSettings.DefaultFromAddress"/> and <see cref="SmtpSettings.DefaultFromName"/>: read as
/// a fallback below <c>Endatix:Integrations:Email:Sender</c>, with one warning at startup when either is set.
/// </summary>
internal sealed class DeprecatedSmtpSender(
    IOptions<SmtpSettings> smtpSettings,
    ILogger<DeprecatedSmtpSender> logger) : IHostedService
{
    /// <summary>
    /// The deprecated values, as a sender fallback. A name with a control character is dropped, by the rule
    /// <c>Sender:Name</c> follows, so the brand name is used instead.
    /// </summary>
    internal static EmailSenderSettings Read(SmtpSettings settings) => new()
    {
        Address = settings.DefaultFromAddress,
        Name = EmailSenderSettingsRules.IsValidName(settings.DefaultFromName) ? settings.DefaultFromName : null,
    };

    public Task StartAsync(CancellationToken cancellationToken)
    {
        var settings = smtpSettings.Value;
        if (!string.IsNullOrWhiteSpace(settings.DefaultFromAddress) || !string.IsNullOrWhiteSpace(settings.DefaultFromName))
        {
            logger.LogDeprecatedSmtpSenderKeys();
        }

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
