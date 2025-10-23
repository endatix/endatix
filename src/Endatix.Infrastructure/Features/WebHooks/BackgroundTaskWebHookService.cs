using Endatix.Core.Features.WebHooks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Endatix.Infrastructure.Features.WebHooks;

/// <summary>
/// Handles the queuing of WebHook messages for asynchronous processing in the background.
/// </summary>
public class BackgroundTaskWebHookService(
    ILogger<BackgroundTaskWebHookService> logger,
    IBackgroundTasksQueue backgroundQueue,
    IOptions<WebHookSettings> webHookOptions,
    WebHookServer httpServer) : IWebHookService
{
    private readonly WebHookSettings _webHookSettings = webHookOptions.Value;

    /// <summary>
    /// Initiates the asynchronous processing of a WebHook message by adding it to the background queue.
    /// </summary>
    /// <param name="tenantId">The tenant ID for which to process the webhook.</param>
    /// <param name="message">The WebHook message to be processed in the background.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task EnqueueWebHookAsync<TPayload>(long tenantId, WebHookMessage<TPayload> message, CancellationToken cancellationToken) where TPayload : notnull
    {
        var eventSetting = GetEventSetting(tenantId, message.operation.EventName);
        if (!eventSetting.IsEnabled)
        {
            logger.LogTrace("WebHook for {eventName} event is disabled. Skipping processing...", message.operation.EventName);
            return;
        }

        var endpoints = eventSetting.GetAllEndpoints();
        if (!endpoints.Any())
        {
            logger.LogTrace("No webhook endpoints found for {eventName} event. Skipping processing...", message.operation.EventName);
            return;
        }

        foreach (var endpoint in endpoints)
        {
            await backgroundQueue.EnqueueAsync(async token =>
            {
                var instructions = TaskInstructions.FromEndpoint(endpoint);
                var result = await httpServer.FireWebHookAsync(message, instructions, token);
            });
        }
    }

    private WebHookSettings.EventSetting GetEventSetting(long tenantId, string eventName)
    {
        if (!_webHookSettings.Tenants.TryGetValue(tenantId, out var tenantConfig))
        {
            return new WebHookSettings.EventSetting { EventName = eventName, IsEnabled = false };
        }

        var tenantEvents = tenantConfig.Events;

        return eventName switch
        {
            WebHooksPlugin.EventNames.FORM_CREATED => tenantEvents.FormCreated,
            WebHooksPlugin.EventNames.FORM_UPDATED => tenantEvents.FormUpdated,
            WebHooksPlugin.EventNames.FORM_ENABLED_STATE_CHANGED => tenantEvents.FormEnabledStateChanged,
            WebHooksPlugin.EventNames.SUBMISSION_COMPLETED => tenantEvents.SubmissionCompleted,
            WebHooksPlugin.EventNames.FORM_DELETED => tenantEvents.FormDeleted,
            _ => throw new ArgumentException($"Unknown event name: {eventName}", nameof(eventName))
        };
    }
}