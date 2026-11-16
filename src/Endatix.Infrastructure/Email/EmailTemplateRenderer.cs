using Ardalis.GuardClauses;
using Endatix.Core;
using Endatix.Core.Branding;
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
/// Every template gets the <see cref="DeploymentBrand.TemplateVariables"/>; the caller's metadata wins on a clash.
/// </remarks>
public sealed class EmailTemplateRenderer(
    IRepository<EmailTemplate> templateRepository,
    IOptions<EmailTemplateSettings> emailTemplateSettings,
    DeploymentBrand brand)
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

        // The pack's address replaces the row's, so the operator's per-template config still wins over both.
        var fromAddress = EmailTemplateFromAddress.Resolve(
            emailTemplateSettings.Value,
            template.Name,
            brand.Email.FromAddress ?? template.FromAddress);

        var rendered = template.Render(
            email.To,
            Variables(email),
            subject: email.Subject,
            from: fromAddress);
        rendered.FromName = brand.Email.SenderName;
        return rendered;
    }

    private Dictionary<string, string> Variables(EmailWithTemplate email)
    {
        var variables = new Dictionary<string, string>(brand.TemplateVariables);
        foreach (var (key, value) in email.Metadata ?? [])
        {
            variables[key] = value?.ToString() ?? string.Empty;
        }

        return variables;
    }
}
