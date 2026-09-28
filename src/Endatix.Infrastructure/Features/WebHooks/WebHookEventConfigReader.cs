using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Core.Specifications;
using Endatix.Infrastructure.Utils;
using Microsoft.Extensions.Logging;

namespace Endatix.Infrastructure.Features.WebHooks;

/// <summary>
/// Reads the webhook configuration of one event. The form's own configuration wins; a form without one falls
/// back to its tenant's.
/// </summary>
/// <remarks>
/// The one place that lookup lives, so inline delivery, the fan-out to webhook jobs and each job at send time all
/// resolve an event's endpoints the same way.
/// </remarks>
public sealed class WebHookEventConfigReader(
    IRepository<Form> formRepository,
    IRepository<TenantSettings> tenantSettingsRepository,
    ILogger<WebHookEventConfigReader> logger)
{
    /// <summary>
    /// The configuration of <paramref name="eventName"/> (snake case, e.g. <c>submission_completed</c>), or
    /// <see langword="null"/> when neither the form nor the tenant configures it.
    /// </summary>
    public async Task<WebHookEventConfig?> GetEventConfigAsync(
        long tenantId,
        string eventName,
        long? formId,
        CancellationToken cancellationToken)
    {
        var config = await GetConfigAsync(tenantId, formId, cancellationToken);
        if (config is not null)
        {
            // Event names are snake case on the wire and Pascal case as configuration keys, e.g.
            // "form_created" -> "FormCreated".
            var pascalCaseEventName = StringUtils.ToPascalCase(eventName);

            if (config.Events.TryGetValue(pascalCaseEventName, out var eventConfig))
            {
                return eventConfig;
            }
        }

        logger.LogTrace("No webhook configuration found for event {EventName}, tenant {TenantId}, form {FormId}", eventName, tenantId, formId);
        return null;
    }

    private async Task<WebHookConfiguration?> GetConfigAsync(long tenantId, long? formId, CancellationToken cancellationToken)
    {
        WebHookConfiguration? config = null;

        if (formId.HasValue)
        {
            var form = await formRepository.GetByIdAsync(formId.Value, cancellationToken);
            if (form is not null && !string.IsNullOrEmpty(form.WebHookSettingsJson))
            {
                config = form.WebHookSettings;
            }
        }

        if (config is null)
        {
            var tenantSettings = await tenantSettingsRepository.FirstOrDefaultAsync(
                new TenantSettingsByTenantIdSpec(tenantId),
                cancellationToken);

            if (tenantSettings is not null && !string.IsNullOrEmpty(tenantSettings.WebHookSettingsJson))
            {
                config = tenantSettings.WebHookSettings;
            }
        }

        return config;
    }
}
