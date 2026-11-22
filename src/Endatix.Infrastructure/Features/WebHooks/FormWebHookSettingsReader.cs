using Endatix.Core.Abstractions.Repositories;
using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Core.Specifications;

namespace Endatix.Infrastructure.Features.WebHooks;

/// <summary>
/// Reads a form's own webhook configuration from its settings: the webhooks section, or <c>Forms.WebHookSettingsJson</c>
/// when the two differ, until that column is dropped (endatix/endatix#1784; <see cref="FormSettings.WebHooksOf"/>).
/// </summary>
public sealed class FormWebHookSettingsReader(IRepository<Form> formRepository, IFormSettingsRepository formSettingsRepository)
{
    /// <summary>
    /// The form's own configuration, or <see langword="null"/> when the form stores none, so its tenant's applies.
    /// </summary>
    /// <remarks>Scoped to the tenant explicitly: in a background job nothing else scopes this read, and a form of
    /// another tenant must not lend its endpoints to this one's event.</remarks>
    public async Task<WebHookConfiguration?> ReadAsync(long formId, long tenantId, CancellationToken cancellationToken)
    {
        var form = await formRepository.FirstOrDefaultAsync(
            new FormSpecifications.ByIdForTenant(formId, tenantId),
            cancellationToken);
        if (form is null || string.IsNullOrWhiteSpace(form.WebHookSettingsJson))
        {
            return null; // no settings read: a form without webhooks of its own sends with its tenant's
        }

        return (await formSettingsRepository.ReadAsync(form, cancellationToken)).WebHooksOf(form);
    }
}
