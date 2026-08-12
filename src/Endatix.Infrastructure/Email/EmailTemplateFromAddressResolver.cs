using Endatix.Core.Abstractions;
using Endatix.Core.Configuration;
using Endatix.Core.Features.Email;
using Microsoft.Extensions.Options;

namespace Endatix.Infrastructure.Email;

/// <summary>
/// Resolves effective sender addresses using email template configuration.
/// </summary>
internal sealed class EmailTemplateFromAddressResolver(IOptions<EmailTemplateSettings> settings)
    : IEmailTemplateFromAddressResolver
{
    /// <inheritdoc />
    public string Resolve(string templateName, string databaseFromAddress) =>
        EmailTemplateFromAddress.ResolveEffectiveFromAddress(
            settings.Value,
            templateName,
            databaseFromAddress);
}
