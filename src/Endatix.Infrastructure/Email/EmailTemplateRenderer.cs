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
/// <remarks>
/// Every template gets the <see cref="EmailVariables"/>; the caller's metadata wins on a clash. Every email sends
/// with the <see cref="DeploymentSender"/>'s display name.
/// </remarks>
public sealed class EmailTemplateRenderer(
    IRepository<EmailTemplate> templateRepository,
    IOptions<EmailTemplateSettings> emailTemplateSettings,
    EmailVariables variables,
    DeploymentSender sender)
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

        var fromAddress = EmailTemplateFromAddress.Resolve(emailTemplateSettings.Value, template, sender);

        var rendered = template.Render(
            email.To,
            variables.ForTemplate(email.Metadata),
            subject: email.Subject,
            from: fromAddress);
        rendered.FromName = sender.Name;
        return rendered;
    }

    /// <summary>
    /// The dynamic data for a provider template (<see cref="EmailWithTemplate.IsExternal"/>): the same variables a
    /// stored template gets, with the caller's metadata values kept as they are.
    /// </summary>
    /// <param name="email">The email sent with a provider template.</param>
    /// <returns>The variables merged with the email's metadata.</returns>
    public Dictionary<string, object> ProviderTemplateData(EmailWithTemplate email)
    {
        Guard.Against.Null(email);
        return variables.ForProvider(email.Metadata);
    }
}
