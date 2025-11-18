using Endatix.Core.Entities;
using Endatix.Core.Features.WebHooks;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Core.Specifications;
using Endatix.Infrastructure.Utils;
using Microsoft.Extensions.Logging;

namespace Endatix.Infrastructure.Features.WebHooks;

/// <summary>
/// Handles the queuing of WebHook messages for asynchronous processing in the background.
/// </summary>
public class BackgroundTaskWebHookService(
    ILogger<BackgroundTaskWebHookService> logger,
    IBackgroundTasksQueue backgroundQueue,
    IRepository<Form> formRepository,
    IRepository<TenantSettings> tenantSettingsRepository,
    WebHookServer httpServer) : IWebHookService
{

    /// <summary>
    /// Initiates the asynchronous processing of a WebHook message by adding it to the background queue.
    /// </summary>
    /// <param name="tenantId">The tenant ID for which to process the webhook.</param>
    /// <param name="message">The WebHook message to be processed in the background.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <param name="formId">Optional form ID to use form-specific webhook configuration.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task EnqueueWebHookAsync<TPayload>(long tenantId, WebHookMessage<TPayload> message, CancellationToken cancellationToken, long? formId = null) where TPayload : notnull
    {
        var eventConfig = await GetEventConfigAsync(tenantId, message.operation.EventName, formId, cancellationToken);
        if (eventConfig == null || !eventConfig.IsEnabled)
        {
            logger.LogTrace("WebHook for {eventName} event is disabled or not configured. Skipping processing...", message.operation.EventName);
            return;
        }

        var endpoints = eventConfig.WebHookEndpoints;
        if (endpoints == null || !endpoints.Any())
        {
            logger.LogTrace("No webhook endpoints found for {eventName} event. Skipping processing...", message.operation.EventName);
            return;
        }

        foreach (var endpoint in endpoints)
        {
            await backgroundQueue.EnqueueAsync(async token =>
            {
                var instructions = TaskInstructions.FromWebHookEndpointConfig(endpoint);
                var result = await httpServer.FireWebHookAsync(message, instructions, token);
            });
        }
    }

    /// <summary>
    /// Retrieves the webhook event configuration from the database.
    /// Form-specific configuration takes precedence over tenant-level configuration.
    /// </summary>
    private async Task<WebHookEventConfig?> GetEventConfigAsync(long tenantId, string eventName, long? formId, CancellationToken cancellationToken)
    {
        var config = await GetConfigAsync(tenantId, formId, cancellationToken);
        if (config != null)
        {
            // Convert snake_case event name to PascalCase for dictionary lookup
            // e.g., "form_created" -> "FormCreated"
            var pascalCaseEventName = StringUtils.ToPascalCase(eventName);

            if (config.Events.TryGetValue(pascalCaseEventName, out var eventConfig))
            {
                return eventConfig;
            }
        }

        logger.LogTrace("No webhook configuration found for event {eventName}, tenant {tenantId}, form {formId}", eventName, tenantId, formId);
        return null;
    }

    private async Task<WebHookConfiguration?> GetConfigAsync(long tenantId, long? formId, CancellationToken cancellationToken)
    {
        WebHookConfiguration? config = null;

        // Try to get form-specific configuration first if formId is provided
        if (formId.HasValue)
        {
            var form = await formRepository.GetByIdAsync(formId.Value, cancellationToken);
            if (form != null && !string.IsNullOrEmpty(form.WebHookSettingsJson))
            {
                config = form.WebHookSettings;
            }
        }

        // Fall back to tenant-level configuration if no form config
        if (config == null)
        {
            var tenantSettings = await tenantSettingsRepository.FirstOrDefaultAsync(
                new TenantSettingsByTenantIdSpec(tenantId),
                cancellationToken);

            if (tenantSettings != null && !string.IsNullOrEmpty(tenantSettings.WebHookSettingsJson))
            {
                config = tenantSettings.WebHookSettings;
            }
        }

        return config;
    }
}