using Endatix.Core.Features.WebHooks;
using Microsoft.Extensions.Options;

namespace Endatix.Infrastructure.Features.WebHooks;

/// <summary>
/// Represents a service for handling background tasks related to WebHooks.
/// </summary>
/// <typeparam name="TPayload">The type of payload to be sent with the WebHook.</typeparam>
internal class BackgroundTaskWebHookService<TPayload>(
    IBackgroundTasksQueue backgroundQueue,
    IOptions<WebHookSettings> webHookOptions,
    WebHookServer httpServer) : IWebHookService<TPayload>
{
    private readonly WebHookSettings _webHookSettings = webHookOptions.Value;

    /// <summary>
    /// Enqueues a WebHook message for asynchronous processing.
    /// </summary>
    /// <param name="message">The WebHook message to be enqueued.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task EnqueueWebHookAsync(WebHookMessage<TPayload> message, CancellationToken cancellationToken)
    {
        await backgroundQueue.EnqueueAsync(async token =>
         {
             var destinationUrl = _webHookSettings.SubmissionCompleted.WebHookUrls?.FirstOrDefault();
             if (!string.IsNullOrEmpty(destinationUrl))
             {
                 WebHookProps webHookProps = new(destinationUrl);
                 var result = await httpServer.FireWebHookAsync(message, webHookProps, token);
             }
         });

        return;
    }

}

/// <summary>
/// Represents properties for a WebHook.
/// </summary>
public class WebHookProps
{
    public WebHookProps(string uri)
    {
        Uri = uri;
    }
    public string Uri { get; init; }
}
