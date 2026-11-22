using Endatix.Core.Abstractions.Repositories;
using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Core.Specifications;

namespace Endatix.Infrastructure.Features.WebHooks;

/// <summary>
/// Reads a form's own webhook configuration: the webhooks section of its settings, or, for a form with no section yet
/// (one an older instance wrote during a rolling deploy), <c>Forms.WebHookSettingsJson</c> until that column is dropped
/// (endatix/endatix#1784).
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
        return form is null ? null : ConfigOf(form, await formSettingsRepository.ReadAsync(form, cancellationToken));
    }

    // Any stored document replaces the tenant's, even one with no events, as the column always did.
    private static WebHookConfiguration? ConfigOf(Form form, FormSettings settings)
    {
        if (settings.WebHooksJson is not null)
        {
            return settings.WebHooks;
        }

        return string.IsNullOrEmpty(form.WebHookSettingsJson) ? null : form.WebHookSettings;
    }
}
