using Ardalis.GuardClauses;
using Endatix.Core;
using Endatix.Core.Configuration;
using Endatix.Core.Entities;
using Endatix.Core.Features.Email;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Core.Specifications;
using Microsoft.Extensions.Options;

namespace Endatix.Infrastructure.Email;

/// <summary>
/// Renders database-backed email templates into provider-ready email bodies.
/// </summary>
public sealed class EmailTemplateRenderer(
    IRepository<EmailTemplate> templateRepository,
    IOptions<EmailTemplateSettings> emailTemplateSettings)
{
    /// <summary>
    /// Renders a database-backed email template into a provider-ready email body.
    /// </summary>
    /// <param name="email">The email with template to render.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The rendered email body.</returns>
    public async Task<EmailWithBody> RenderAsync(EmailWithTemplate email, CancellationToken cancellationToken = default)
    {
        Guard.Against.Null(email);
        Guard.Against.NullOrWhiteSpace(email.To);
        Guard.Against.NullOrWhiteSpace(email.TemplateId);

        var template = await templateRepository.FirstOrDefaultAsync(
            new EmailTemplateByNameSpec(email.TemplateId),
            cancellationToken);

        if (template is null)
        {
            throw new InvalidOperationException($"Email template '{email.TemplateId}' not found in database");
        }

        var variables = (email.Metadata ?? [])
            .ToDictionary(kvp => kvp.Key, kvp => kvp.Value?.ToString() ?? string.Empty);

        var fromAddress = EmailTemplateFromAddress.Resolve(
            emailTemplateSettings.Value,
            template.Name,
            template.FromAddress);

        return template.Render(
            email.To,
            variables,
            subject: email.Subject,
            from: fromAddress);
    }
}
