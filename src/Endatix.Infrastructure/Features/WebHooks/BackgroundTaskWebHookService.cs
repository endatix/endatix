using System.Text;
using System.Text.Json;
using Endatix.Core.Features.WebHooks;
using Microsoft.Extensions.Logging;

namespace Endatix.Infrastructure.Features.WebHooks;

internal class BackgroundTaskWebHookService<TPayload>(
    IBackgroundTasksQueue backgroundQueue,
    WebHookServer httpServer) : IWebHookService<TPayload>
{
    public async Task EnqueueWebHookAsync(WebHookMessage<TPayload> message, CancellationToken cancellationToken)
    {
        await backgroundQueue.EnqueueAsync(async token =>
         {

             // https://webhook.site/3cbd0d84-9e49-4518-a18b-aa82f8fc6305
             // https://66bf-212-5-158-18.ngrok-free.app
             WebHookProps webHookProps = new("https://webhook.site/3cbd0d84-9e49-4518-a18b-aa82f8fc6305");
             var result = await httpServer.FireWebHookAsync(message, webHookProps, token);
         });

        return;
    }

}

public class WebHookProps
{
    public WebHookProps(string uri)
    {
        Uri = uri;
    }
    public string Uri { get; init; }
}
