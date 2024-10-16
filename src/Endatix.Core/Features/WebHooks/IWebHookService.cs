namespace Endatix.Core.Features.WebHooks;

public interface IWebHookService<TPayload> {
    Task EnqueueWebHookAsync(WebHookMessage<TPayload> message, CancellationToken cancellationToken);
}