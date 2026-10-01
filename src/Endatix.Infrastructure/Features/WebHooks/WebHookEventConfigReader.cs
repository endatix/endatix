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
    /// The configuration of the event <paramref name="lookup"/> names, or <see langword="null"/> when neither the
    /// form nor the tenant configures it.
    /// </summary>
    public async Task<WebHookEventConfig?> GetEventConfigAsync(
        WebHookEventLookup lookup,
        CancellationToken cancellationToken)
    {
        var config = await FormConfigAsync(lookup.FormId, cancellationToken)
            ?? await TenantConfigAsync(lookup.TenantId, cancellationToken);

        // Event names are snake case on the wire and Pascal case as configuration keys, e.g.
        // "form_created" -> "FormCreated".
        if (config is not null && config.Events.TryGetValue(StringUtils.ToPascalCase(lookup.EventName), out var eventConfig))
        {
            return eventConfig;
        }

        LogNotConfigured(lookup);
        return null;
    }

    private void LogNotConfigured(WebHookEventLookup lookup) =>
        logger.LogTrace(
            "No webhook configuration found for event {EventName}, tenant {TenantId}, form {FormId}",
            lookup.EventName,
            lookup.TenantId,
            lookup.FormId);

    private async Task<WebHookConfiguration?> FormConfigAsync(long? formId, CancellationToken cancellationToken)
    {
        if (formId is not { } id)
        {
            return null;
        }

        var form = await formRepository.GetByIdAsync(id, cancellationToken);
        return form is not null && !string.IsNullOrEmpty(form.WebHookSettingsJson) ? form.WebHookSettings : null;
    }

    private async Task<WebHookConfiguration?> TenantConfigAsync(long tenantId, CancellationToken cancellationToken)
    {
        var tenantSettings = await tenantSettingsRepository.FirstOrDefaultAsync(
            new TenantSettingsByTenantIdSpec(tenantId),
            cancellationToken);
        return tenantSettings is not null && !string.IsNullOrEmpty(tenantSettings.WebHookSettingsJson)
            ? tenantSettings.WebHookSettings
            : null;
    }
}

/// <summary>Which event's webhook configuration to read, and for which tenant and form.</summary>
/// <param name="TenantId">The tenant whose configuration applies when the form has none.</param>
/// <param name="EventName">The event, in snake case, e.g. <c>submission_completed</c>.</param>
/// <param name="FormId">The form whose own configuration wins, if the event concerns one.</param>
public sealed record WebHookEventLookup(long TenantId, string EventName, long? FormId);
