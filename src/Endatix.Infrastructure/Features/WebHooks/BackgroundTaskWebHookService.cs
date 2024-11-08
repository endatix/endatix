using Endatix.Core.Features.WebHooks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Endatix.Infrastructure.Features.WebHooks;

/// <summary>
/// Handles the queuing of WebHook messages for asynchronous processing in the background.
/// </summary>
internal class BackgroundTaskWebHookService(
    ILogger<BackgroundTaskWebHookService> logger,
    IBackgroundTasksQueue backgroundQueue,
    IOptions<WebHookSettings> webHookOptions,
    WebHookServer httpServer) : IWebHookService
{
    private readonly WebHookSettings _webHookSettings = webHookOptions.Value;

    /// <summary>
    /// Initiates the asynchronous processing of a WebHook message by adding it to the background queue.
    /// </summary>
    /// <param name="message">The WebHook message to be processed in the background.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task EnqueueWebHookAsync<TPayload>(WebHookMessage<TPayload> message, CancellationToken cancellationToken) where TPayload : notnull
    {
        if (!_webHookSettings.Events.FormSubmitted.IsEnabled)
        {
            logger.LogTrace("WebHook for FormSubmitted event is disabled. Skipping processing...");
            return;
        }
        var destinationUrls = _webHookSettings.Events.FormSubmitted?.WebHookUrls;
        if (destinationUrls is null || !destinationUrls.Any())
        {
            logger.LogTrace("No destination URLs found for FormSubmitted event. Skipping processing...");
            return;
        }

        foreach (var destinationUrl in destinationUrls)
        {
            await backgroundQueue.EnqueueAsync(async token =>
             {
                 TaskInstructions instructions = new(destinationUrl);
                 var result = await httpServer.FireWebHookAsync(message, instructions, token);
             });
        }

        return;
    }
}